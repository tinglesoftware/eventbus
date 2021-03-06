﻿using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Amazon.Sqs
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using
    /// Amazon SQS and Amazon SNS as the transport.
    /// </summary>
    [TransportName(TransportNames.AmazonSqs)]
    public class AmazonSqsTransport : EventBusTransportBase<AmazonSqsTransportOptions>, IDisposable
    {
        private readonly Dictionary<Type, string> topicArnsCache = new Dictionary<Type, string>();
        private readonly SemaphoreSlim topicArnsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<(string, bool), string> queueUrlsCache = new Dictionary<(string, bool), string>();
        private readonly SemaphoreSlim queueUrlsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly CancellationTokenSource stoppingCts = new CancellationTokenSource();
        private readonly List<Task> receiverTasks = new List<Task>();
        private readonly AmazonSimpleNotificationServiceClient snsClient;
        private readonly AmazonSQSClient sqsClient;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="optionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AmazonSqsTransport(IServiceScopeFactory serviceScopeFactory,
                                  IOptions<EventBusOptions> optionsAccessor,
                                  IOptions<AmazonSqsTransportOptions> transportOptionsAccessor,
                                  ILoggerFactory loggerFactory)
            : base(serviceScopeFactory, optionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            snsClient = new AmazonSimpleNotificationServiceClient(credentials: TransportOptions.Credentials,
                                                                  clientConfig: TransportOptions.SnsConfig);

            sqsClient = new AmazonSQSClient(credentials: TransportOptions.Credentials,
                                            clientConfig: TransportOptions.SqsConfig);
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                          CancellationToken cancellationToken = default)
        {
            _ = await snsClient.ListTopicsAsync(cancellationToken);
            var prefix = BusOptions.Naming.Scope ?? "";
            _ = await sqsClient.ListQueuesAsync(prefix, cancellationToken);
            return true;
        }

        /// <inheritdoc/>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            var registrations = GetRegistrations();
            foreach (var ereg in registrations)
            {
                foreach (var creg in ereg.Consumers)
                {
                    var queueUrl = await GetQueueUrlAsync(ereg: ereg,
                                                          creg: creg,
                                                          deadletter: false,
                                                          cancellationToken: cancellationToken);
                    var t = ReceiveAsync(ereg: ereg,
                                         creg: creg,
                                         queueUrl: queueUrl,
                                         cancellationToken: stoppingCts.Token);
                    receiverTasks.Add(t);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            // Stop called without start or there was no consumers registered
            if (receiverTasks.Count == 0) return;

            try
            {
                // Signal cancellation to the executing methods/tasks
                stoppingCts.Cancel();
            }
            finally
            {
                // Wait until the tasks complete or the stop token triggers
                var tasks = receiverTasks.Concat(new[] { Task.Delay(Timeout.Infinite, cancellationToken), });
                await Task.WhenAny(tasks);
            }
        }

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                EventRegistration registration,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Amazon SNS does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            using var ms = new MemoryStream();
            await SerializeAsync(body: ms,
                                 @event: @event,
                                 registration: registration,
                                 scope: scope,
                                 cancellationToken: cancellationToken);

            // get the topic arn and send the message
            var topicArn = await GetTopicArnAsync(registration, cancellationToken);
            var message = Encoding.UTF8.GetString(ms.ToArray());
            var request = new PublishRequest(topicArn: topicArn, message: message);
            request.SetAttribute(AttributeNames.ContentType, @event.ContentType.ToString())
                   .SetAttribute(AttributeNames.CorrelationId, @event.CorrelationId)
                   .SetAttribute(AttributeNames.RequestId, @event.RequestId)
                   .SetAttribute(AttributeNames.InitiatorId, @event.InitiatorId)
                   .SetAttribute(AttributeNames.ActivityId, Activity.Current?.Id);
            Logger.LogInformation("Sending {Id} to '{TopicArn}'. Scheduled: {Scheduled}", @event.Id, topicArn, scheduled);
            var response = await snsClient.PublishAsync(request: request, cancellationToken: cancellationToken);
            response.EnsureSuccess();

            // return the sequence number
            return scheduled != null ? response.SequenceNumber : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                       EventRegistration registration,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        {
            // log warning when doing batch
            Logger.LogWarning("Amazon SNS does not support batching. The events will be looped through one by one");

            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Amazon SNS does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var sequenceNumbers = new List<string>();

            // work on each event
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                await SerializeAsync(body: ms,
                                     @event: @event,
                                     registration: registration,
                                     scope: scope,
                                     cancellationToken: cancellationToken);

                // get the topic arn and send the message
                var topicArn = await GetTopicArnAsync(registration, cancellationToken);
                var message = Encoding.UTF8.GetString(ms.ToArray());
                var request = new PublishRequest(topicArn: topicArn, message: message);
                request.SetAttribute(AttributeNames.ContentType, @event.ContentType.ToString())
                       .SetAttribute(AttributeNames.CorrelationId, @event.CorrelationId)
                       .SetAttribute(AttributeNames.RequestId, @event.RequestId)
                       .SetAttribute(AttributeNames.InitiatorId, @event.InitiatorId)
                       .SetAttribute(AttributeNames.ActivityId, Activity.Current?.Id);
                Logger.LogInformation("Sending {Id} to '{TopicArn}'. Scheduled: {Scheduled}", @event.Id, topicArn, scheduled);
                var response = await snsClient.PublishAsync(request: request, cancellationToken: cancellationToken);
                response.EnsureSuccess();

                // collect the sequence number
                sequenceNumbers.Add(response.SequenceNumber);
            }

            // return the sequence numbers
            return scheduled != null ? sequenceNumbers : null;
        }


        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Amazon SNS does not support canceling published messages.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Amazon SNS does not support canceling published messages.");
        }

        private async Task<string> GetTopicArnAsync(EventRegistration reg, CancellationToken cancellationToken)
        {
            await topicArnsCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!topicArnsCache.TryGetValue(reg.EventType, out var topicArn))
                {
                    // ensure topic is created, then add it's arn to the cache
                    var name = reg.EventName;
                    topicArn = await CreateTopicIfNotExistsAsync(ereg: reg, topicName: name, cancellationToken: cancellationToken);
                    topicArnsCache[reg.EventType] = topicArn;
                }

                return topicArn;
            }
            finally
            {
                topicArnsCacheLock.Release();
            }
        }

        private async Task<string> GetQueueUrlAsync(EventRegistration ereg, EventConsumerRegistration creg, bool deadletter, CancellationToken cancellationToken)
        {
            await queueUrlsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var topicName = ereg.EventName;
                var queueName = creg.ConsumerName;
                if (deadletter) queueName += TransportOptions.DeadLetterSuffix;

                var key = $"{topicName}/{queueName}";
                if (!queueUrlsCache.TryGetValue((key, deadletter), out var queueUrl))
                {
                    // ensure queue is created before creating subscription
                    queueUrl = await CreateQueueIfNotExistsAsync(creg: creg, queueName: queueName, cancellationToken: cancellationToken);

                    // for non deadletter, we need to ensure the topic exists and the queue is subscribed to it
                    if (!deadletter)
                    {
                        // ensure topic is created before creating the subscription
                        var topicArn = await CreateTopicIfNotExistsAsync(ereg: ereg, topicName: topicName, cancellationToken: cancellationToken);

                        // create subscription from the topic to the queue
                        await snsClient.SubscribeQueueAsync(topicArn: topicArn, sqsClient, queueUrl);
                    }

                    queueUrlsCache[(key, deadletter)] = queueUrl;
                }

                return queueUrl;
            }
            finally
            {
                queueUrlsCacheLock.Release();
            }
        }

        private async Task<string> CreateTopicIfNotExistsAsync(EventRegistration ereg, string topicName, CancellationToken cancellationToken)
        {
            // check if the topic exists
            var topic = await snsClient.FindTopicAsync(topicName: topicName);
            if (topic != null) return topic.TopicArn;

            // if entity creation is not enabled, throw exception
            if (!TransportOptions.EnableEntityCreation)
            {
                throw new InvalidOperationException("Entity creation is diabled. Required topic could not be created.");
            }

            // create the topic
            var request = new CreateTopicRequest(name: topicName);
            TransportOptions.SetupCreateTopicRequest?.Invoke(ereg, request);
            var response = await snsClient.CreateTopicAsync(request: request, cancellationToken: cancellationToken);
            response.EnsureSuccess();

            return response.TopicArn;
        }

        private async Task<string> CreateQueueIfNotExistsAsync(EventConsumerRegistration creg, string queueName, CancellationToken cancellationToken)
        {
            // check if the queue exists
            var urlResponse = await sqsClient.GetQueueUrlAsync(queueName: queueName, cancellationToken);
            if (urlResponse != null && urlResponse.Successful()) return urlResponse.QueueUrl;

            // if entity creation is not enabled, throw exception
            if (!TransportOptions.EnableEntityCreation)
            {
                throw new InvalidOperationException("Entity creation is diabled. Required queue could not be created.");
            }

            // create the queue
            var request = new CreateQueueRequest(queueName: queueName);
            TransportOptions.SetupCreateQueueRequest?.Invoke(creg, request);
            var response = await sqsClient.CreateQueueAsync(request: request, cancellationToken: cancellationToken);
            response.EnsureSuccess();

            return response.QueueUrl;
        }

        private async Task ReceiveAsync(EventRegistration ereg, EventConsumerRegistration creg, string queueUrl, CancellationToken cancellationToken)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
            var method = mt.MakeGenericMethod(ereg.EventType, creg.ConsumerType);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await sqsClient.ReceiveMessageAsync(queueUrl, cancellationToken);
                    response.EnsureSuccess();
                    var messages = response.Messages;

                    // if the response is empty, introduce a delay
                    if (messages.Count == 0)
                    {
                        var delay = TransportOptions.EmptyResultsDelay;
                        Logger.LogTrace("No messages on '{QueueUrl}', delaying check for {Delay}", queueUrl, delay);
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        Logger.LogDebug("Received {MessageCount} messages on '{QueueUrl}'", messages.Count, queueUrl);
                        using var scope = CreateScope(); // shared
                        foreach (var message in messages)
                        {
                            await (Task)method.Invoke(this, new object[] { ereg, creg, queueUrl, message, cancellationToken, });
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Ignore
                    // Thrown from inside Task.Delay(...) if cancellation token is canceled
                }
                catch (OperationCanceledException)
                {
                    // Ignore
                    // Thrown from calls to cancellationToken.ThrowIfCancellationRequested(...)
                }
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration ereg,
                                                                     EventConsumerRegistration creg,
                                                                     string queueUrl,
                                                                     Message message,
                                                                     CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventConsumer<TEvent>
        {
            var messageId = message.MessageId;
            message.TryGetAttribute(AttributeNames.CorrelationId, out var correlationId);
            message.TryGetAttribute(AttributeNames.SequenceNumber, out var sequenceNumber);
            message.TryGetAttribute(AttributeNames.ActivityId, out var parentActivityId);

            using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                              correlationId: correlationId,
                                                              sequenceNumber: sequenceNumber,
                                                              extras: new Dictionary<string, string>
                                                              {
                                                                  ["QueueUrl"] = queueUrl,
                                                                  ["ReceiptHandle"] = message.ReceiptHandle,
                                                                  [nameof(message.MD5OfBody)] = message.MD5OfBody,
                                                                  [nameof(message.MD5OfMessageAttributes)] = message.MD5OfMessageAttributes,
                                                              });

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId);
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
            activity?.AddTag(ActivityTagNames.MessagingDestination, ereg.EventName);
            activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue");
            activity?.AddTag(ActivityTagNames.MessagingUrl, queueUrl);

            Logger.LogDebug("Processing '{MessageId}' from '{QueueUrl}'", messageId, queueUrl);
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(message.Body));
            message.TryGetAttribute("Content-Type", out var contentType_str);
            var contentType = contentType_str == null ? null : new ContentType(contentType_str);

            using var scope = CreateScope();
            var context = await DeserializeAsync<TEvent>(body: ms,
                                                         contentType: contentType,
                                                         registration: ereg,
                                                         scope: scope,
                                                         cancellationToken: cancellationToken);

            Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}' from '{QueueUrl}'",
                                  messageId,
                                  context.Id,
                                  queueUrl);

            var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(creg: creg,
                                                                        @event: context,
                                                                        scope: scope,
                                                                        cancellationToken: cancellationToken);

            if (!successful && creg.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
            {
                // get the queueUrl for the dead letter queue and send the mesage there
                var dlqQueueUrl = await GetQueueUrlAsync(ereg: ereg, creg: creg, deadletter: true, cancellationToken: cancellationToken);
                var dlqRequest = new SendMessageRequest
                {
                    MessageAttributes = message.MessageAttributes,
                    MessageBody = message.Body,
                    QueueUrl = dlqQueueUrl,
                };
                await sqsClient.SendMessageAsync(request: dlqRequest, cancellationToken: cancellationToken);
            }

            // whether or not successful, always delete the message from the current queue
            Logger.LogTrace("Deleting '{MessageId}' on '{QueueUrl}'", messageId, queueUrl);
            await sqsClient.DeleteMessageAsync(queueUrl: queueUrl,
                                               receiptHandle: message.ReceiptHandle,
                                               cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            stoppingCts.Cancel();
        }
    }
}
