using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;
using MAV = Amazon.SimpleNotificationService.Model.MessageAttributeValue;

namespace Tingle.EventBus.Transports.Amazon.Sqs
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using
    /// Amazon SQS and Amazon SNS as the transport.
    /// </summary>
    [TransportName(TransportNames.AmazonSqs)]
    public class AmazonSqsTransport : EventBusTransportBase<AmazonSqsTransportOptions>
    {
        private readonly Dictionary<Type, string> topicArnsCache = new Dictionary<Type, string>();
        private readonly SemaphoreSlim topicArnsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<string, string> queueUrlsCache = new Dictionary<string, string>();
        private readonly SemaphoreSlim queueUrlsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly CancellationTokenSource receiveCancellationTokenSource = new CancellationTokenSource();
        private readonly AmazonSimpleNotificationServiceClient snsClient;
        private readonly AmazonSQSClient sqsClient;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="optionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AmazonSqsTransport(IHostEnvironment environment,
                                 IServiceScopeFactory serviceScopeFactory,
                                 IOptions<EventBusOptions> optionsAccessor,
                                 IOptions<AmazonSqsTransportOptions> transportOptionsAccessor,
                                 ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, optionsAccessor, transportOptionsAccessor, loggerFactory)
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
            var prefix = BusOptions.Scope ?? "";
            _ = await sqsClient.ListQueuesAsync(prefix, cancellationToken);
            return true;
        }

        /// <inheritdoc/>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = GetConsumerRegistrations();
            Logger.StartingTransport(registrations.Count);
            foreach (var reg in registrations)
            {
                var queueUrl = await GetQueueUrlAsync(reg: reg, cancellationToken: cancellationToken);
                _ = ReceiveAsync(reg, queueUrl);
            }
        }

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.StoppingTransport();
            receiveCancellationTokenSource.Cancel();
            // TODO: figure out a way to wait for notification of termination in all receivers
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Amazon SNS does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   registration: reg,
                                                   scope: scope,
                                                   cancellationToken: cancellationToken);

            // get the topic arn and send the message
            var topicArn = await GetTopicArnAsync(reg, cancellationToken);
            var message = Encoding.UTF8.GetString(ms.ToArray());
            var request = new PublishRequest(topicArn: topicArn, message: message);
            SetAttribute(request, "Content-Type", contentType.ToString());
            SetAttribute(request, nameof(@event.CorrelationId), @event.CorrelationId);
            var response = await snsClient.PublishAsync(request: request, cancellationToken: cancellationToken);
            response.EnsureSuccess();

            // return the sequence number
            return scheduled != null ? response.SequenceNumber : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
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
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var sequenceNumbers = new List<string>();

            // work on each event
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       registration: reg,
                                                       scope: scope,
                                                       cancellationToken: cancellationToken);

                // get the topic arn and send the message
                var topicArn = await GetTopicArnAsync(reg, cancellationToken);
                var message = Encoding.UTF8.GetString(ms.ToArray());
                var request = new PublishRequest(topicArn: topicArn, message: message);
                SetAttribute(request, "Content-Type", contentType.ToString());
                SetAttribute(request, nameof(@event.CorrelationId), @event.CorrelationId);
                var response = await snsClient.PublishAsync(request: request, cancellationToken: cancellationToken);
                response.EnsureSuccess();

                // collect the sequence number
                sequenceNumbers.Add(response.SequenceNumber);
            }

            // return the sequence numbers
            return scheduled != null ? sequenceNumbers : null;
        }


        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Amazon SNS does not support canceling published messages.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
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
                    topicArn = await CreateTopicIfNotExistsAsync(topicName: name, cancellationToken: cancellationToken);
                    topicArnsCache[reg.EventType] = topicArn;
                }

                return topicArn;
            }
            finally
            {
                topicArnsCacheLock.Release();
            }
        }

        private async Task<string> GetQueueUrlAsync(ConsumerRegistration reg, CancellationToken cancellationToken)
        {
            await queueUrlsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var topicName = reg.EventName;
                var queueName = reg.ConsumerName;

                var key = $"{topicName}/{queueName}";
                if (!queueUrlsCache.TryGetValue(key, out var queueUrl))
                {
                    // ensure topic is created before creating the subscription
                    var topicArn = await CreateTopicIfNotExistsAsync(topicName: topicName, cancellationToken: cancellationToken);

                    // ensure queue is created before creating subscription
                    queueUrl = await CreateQueueIfNotExistsAsync(queueName: queueName, cancellationToken: cancellationToken);

                    // create subscription from the topic to the queue
                    await snsClient.SubscribeQueueAsync(topicArn: topicArn, sqsClient, queueUrl);
                    queueUrlsCache[key] = queueUrl;
                }

                return queueUrl;
            }
            finally
            {
                queueUrlsCacheLock.Release();
            }
        }

        private async Task<string> CreateTopicIfNotExistsAsync(string topicName, CancellationToken cancellationToken)
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
            TransportOptions.SetupCreateTopicRequest?.Invoke(request);
            var response = await snsClient.CreateTopicAsync(request: request, cancellationToken: cancellationToken);
            response.EnsureSuccess();

            return response.TopicArn;
        }

        private async Task<string> CreateQueueIfNotExistsAsync(string queueName, CancellationToken cancellationToken)
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
            TransportOptions.SetupCreateQueueRequest?.Invoke(request);
            var response = await sqsClient.CreateQueueAsync(request: request, cancellationToken: cancellationToken);
            response.EnsureSuccess();

            return response.QueueUrl;
        }

        private static void SetAttribute(PublishRequest request, string key, string value)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(value)) return;
            request.MessageAttributes[key] = new MAV { DataType = "String", StringValue = value };
        }

        private static bool TryGetAttribute(Message message, string key, out string value)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
            }

            if (message.Attributes.TryGetValue(key, out value)) return true;

            if (message.MessageAttributes.TryGetValue(key, out var attr))
            {
                value = attr.StringValue;
                return true;
            }

            value = default;
            return false;
        }

        private async Task ReceiveAsync(ConsumerRegistration reg, string queueUrl)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
            var method = mt.MakeGenericMethod(reg.EventType, reg.ConsumerType);
            var cancellationToken = receiveCancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
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
                        await (Task)method.Invoke(this, new object[] { reg, queueUrl, message, cancellationToken, });
                    }
                }
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg,
                                                                     string queueUrl,
                                                                     Message message,
                                                                     CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            TryGetAttribute(message, "CorrelationId", out var correlationId);
            TryGetAttribute(message, "SequenceNumber", out var sequenceNumber);

            using var log_scope = Logger.BeginScope(new Dictionary<string, string>
            {
                ["MesageId"] = message.MessageId,
                ["CorrelationId"] = correlationId,
                ["SequenceNumber"] = sequenceNumber,
            });

            try
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(message.Body));
                TryGetAttribute(message, "Content-Type", out var contentType_str);
                var contentType = new ContentType(contentType_str ?? "text/plain");

                using var scope = CreateScope();
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: reg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);
                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);

                // delete the message from the queue
                await sqsClient.DeleteMessageAsync(queueUrl: queueUrl,
                                                   receiptHandle: message.ReceiptHandle,
                                                   cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event processing failed. Moving to deadletter.");
                // TODO: implement dead lettering in SQS
            }
        }
    }
}
