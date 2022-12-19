using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Mime;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports.Amazon.Sqs;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using
/// Amazon SQS and Amazon SNS as the transport.
/// </summary>
public class AmazonSqsTransport : EventBusTransport<AmazonSqsTransportOptions>, IDisposable
{
    private readonly EventBusConcurrentDictionary<Type, string> topicArnsCache = new();
    private readonly EventBusConcurrentDictionary<QueueCacheKey, string> queueUrlsCache = new();
    private readonly CancellationTokenSource stoppingCts = new();
    private readonly List<Task> receiverTasks = new();
    private readonly Lazy<AmazonSimpleNotificationServiceClient> snsClient;
    private readonly Lazy<AmazonSQSClient> sqsClient;
    private bool disposedValue;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="optionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public AmazonSqsTransport(IServiceScopeFactory serviceScopeFactory,
                              IOptions<EventBusOptions> optionsAccessor,
                              IOptionsMonitor<AmazonSqsTransportOptions> optionsMonitor,
                              ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, optionsAccessor, optionsMonitor, loggerFactory)
    {
        snsClient = new Lazy<AmazonSimpleNotificationServiceClient>(
            () => new AmazonSimpleNotificationServiceClient(credentials: Options.Credentials, clientConfig: Options.SnsConfig));

        sqsClient = new Lazy<AmazonSQSClient>(
            () => new AmazonSQSClient(credentials: Options.Credentials, clientConfig: Options.SqsConfig));
    }

    /// <inheritdoc/>
    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            foreach (var ecr in reg.Consumers.Values)
            {
                var queueUrl = await GetQueueUrlAsync(reg: reg,
                                                      ecr: ecr,
                                                      deadletter: false,
                                                      cancellationToken: cancellationToken).ConfigureAwait(false);
                var t = ReceiveAsync(reg: reg,
                                     ecr: ecr,
                                     queueUrl: queueUrl,
                                     cancellationToken: stoppingCts.Token);
                receiverTasks.Add(t);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
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
            await Task.WhenAny(tasks).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task<ScheduledResult?> PublishCoreAsync<TEvent>(EventContext<TEvent> @event,
                                                                             EventRegistration registration,
                                                                             DateTimeOffset? scheduled = null,
                                                                             CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        var body = await SerializeAsync(scope: scope,
                                        @event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

        string sequenceNumber;
        if (registration.EntityKind != EntityKind.Broadcast)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.SchedulingNotSupportedBySns();
            }

            // get the topic arn and send the message
            var topicArn = await GetTopicArnAsync(registration, cancellationToken).ConfigureAwait(false);
            var request = new PublishRequest(topicArn: topicArn, message: body.ToString()).SetAttributes(@event);
            Logger.SendingToTopic(eventBusId: @event.Id, topicArn: topicArn, scheduled: scheduled);
            var response = await snsClient.Value.PublishAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccess();
            sequenceNumber = response.SequenceNumber;
        }
        else
        {
            // get the queueUrl and prepare the message
            var queueUrl = await GetQueueUrlAsync(registration, cancellationToken: cancellationToken).ConfigureAwait(false);
            var request = new SendMessageRequest(queueUrl: queueUrl, body.ToString()).SetAttributes(@event);

            // if scheduled for later, set the delay in the message
            if (scheduled != null)
            {
                var delay = Math.Max(0, (scheduled.Value - DateTimeOffset.UtcNow).TotalSeconds);

                // cap the delay to 900 seconds (15min) which is the max supported by SQS
                if (delay > 900)
                {
                    Logger.DelayCapped(eventBusId: @event.Id, scheduled: scheduled);
                    delay = 900;
                }

                if (delay > 0)
                {
                    request.DelaySeconds = (int)delay;
                }
            }

            // send the message
            Logger.SendingToQueue(eventBusId: @event.Id, queueUrl: queueUrl, scheduled: scheduled);
            var response = await sqsClient.Value.SendMessageAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccess();
            sequenceNumber = response.SequenceNumber;
        }

        // return the sequence number
        return scheduled != null ? new ScheduledResult(id: sequenceNumber, scheduled: scheduled.Value) : null;
    }

    /// <inheritdoc/>
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                    EventRegistration registration,
                                                                                    DateTimeOffset? scheduled = null,
                                                                                    CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        var sequenceNumbers = new List<string>();

        // log warning when trying to publish scheduled message to a topic
        if (registration.EntityKind == EntityKind.Broadcast)
        {
            // log warning when doing batch
            Logger.BatchingNotSupported();

            // log warning when trying to publish scheduled message to a topic
            if (scheduled != null)
            {
                Logger.SchedulingNotSupportedBySns();
            }

            // work on each event
            foreach (var @event in events)
            {
                var body = await SerializeAsync(scope: scope,
                                                @event: @event,
                                                registration: registration,
                                                cancellationToken: cancellationToken).ConfigureAwait(false);

                // get the topic arn and send the message
                var topicArn = await GetTopicArnAsync(registration, cancellationToken).ConfigureAwait(false);
                var request = new PublishRequest(topicArn: topicArn, message: body.ToString()).SetAttributes(@event);
                Logger.SendingToTopic(eventBusId: @event.Id, topicArn: topicArn, scheduled: scheduled);
                var response = await snsClient.Value.PublishAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(false);
                response.EnsureSuccess();

                // collect the sequence number
                sequenceNumbers.Add(response.SequenceNumber);
            }
        }
        else
        {
            // prepare batch entries
            var entries = new List<SendMessageBatchRequestEntry>(events.Count);
            foreach (var @event in events)
            {
                var body = await SerializeAsync(scope: scope,
                                                @event: @event,
                                                registration: registration,
                                                cancellationToken: cancellationToken).ConfigureAwait(false);

                var entry = new SendMessageBatchRequestEntry(id: @event.Id, messageBody: body.ToString()).SetAttributes(@event);

                // if scheduled for later, set the delay in the message
                if (scheduled != null)
                {
                    var delay = Math.Max(0, (scheduled.Value - DateTimeOffset.UtcNow).TotalSeconds);

                    // cap the delay to 900 seconds (15min) which is the max supported by SQS
                    if (delay > 900)
                    {
                        Logger.DelayCapped(eventBusId: @event.Id, scheduled: scheduled);
                        delay = 900;
                    }

                    if (delay > 0)
                    {
                        entry.DelaySeconds = (int)delay;
                    }
                }

                entries.Add(entry);
            }

            // get the queueUrl and send the messages
            var queueUrl = await GetQueueUrlAsync(registration, cancellationToken: cancellationToken).ConfigureAwait(false);
            var request = new SendMessageBatchRequest(queueUrl: queueUrl, entries: entries);
            var response = await sqsClient.Value.SendMessageBatchAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccess();
            sequenceNumbers = response.Successful.Select(smbre => smbre.SequenceNumber).ToList();
        }

        // return the sequence numbers
        return scheduled != null ? sequenceNumbers.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList() : null;
    }


    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(string id,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Amazon SNS does not support canceling published messages.");
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(IList<string> ids,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Amazon SNS does not support canceling published messages.");
    }

    private Task<string> GetTopicArnAsync(EventRegistration reg, CancellationToken cancellationToken)
        => topicArnsCache.GetOrAddAsync(reg.EventType, (_, ct) => CreateTopicIfNotExistsAsync(topicName: reg.EventName!, reg: reg, cancellationToken: ct), cancellationToken);

    private Task<string> GetQueueUrlAsync(EventRegistration reg, EventConsumerRegistration? ecr = null, bool deadletter = false, CancellationToken cancellationToken = default)
    {
        var key = CreateQueueCacheKey(reg, ecr, deadletter);
        return queueUrlsCache.GetOrAddAsync(key, async (_, ct) =>
        {
            // ensure queue is created before creating subscription
            var queueUrl = await CreateQueueIfNotExistsAsync(queueName: key.Name, reg: reg, ecr: ecr, cancellationToken: ct).ConfigureAwait(false);

            // for non dead-letter broadcast types, we need to ensure the topic exists and the queue is subscribed to it
            if (!deadletter && reg.EntityKind == EntityKind.Broadcast)
            {
                // ensure topic is created before creating the subscription
                var topicName = reg.EventName!;
                var topicArn = await CreateTopicIfNotExistsAsync(topicName: topicName, reg: reg, cancellationToken: ct).ConfigureAwait(false);

                // create subscription from the topic to the queue
                await snsClient.Value.SubscribeQueueAsync(topicArn: topicArn, sqsClient.Value, queueUrl).ConfigureAwait(false);
            }

            return queueUrl;
        }, cancellationToken);
    }

    record QueueCacheKey
    {
        public QueueCacheKey(string name, bool deadletter)
        {
            if (string.IsNullOrWhiteSpace(Name = name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
            }
            Deadletter = deadletter;
        }

        public string Name { get; }
        public bool Deadletter { get; }
    }

    private QueueCacheKey CreateQueueCacheKey(EventRegistration reg, EventConsumerRegistration? ecr, bool deadLetter)
    {
        var name = (reg.EntityKind == EntityKind.Broadcast && ecr is not null)
                 ? $"{reg.EventName}-{ecr.ConsumerName}"
                 : reg.EventName!;
        if (deadLetter) name += Options.DeadLetterSuffix;
        return new QueueCacheKey(name, deadLetter);
    }

    private async Task<string> CreateTopicIfNotExistsAsync(string topicName, EventRegistration reg, CancellationToken cancellationToken)
    {
        // check if the topic exists
        var topic = await snsClient.Value.FindTopicAsync(topicName: topicName).ConfigureAwait(false);
        if (topic != null) return topic.TopicArn;

        // if entity creation is not enabled, throw exception
        if (!Options.EnableEntityCreation)
        {
            throw new InvalidOperationException("Entity creation is disabled. Required topic could not be created.");
        }

        // create the topic
        var request = new CreateTopicRequest(name: topicName);
        Options.SetupCreateTopicRequest?.Invoke(reg, request);
        var response = await snsClient.Value.CreateTopicAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(false);
        response.EnsureSuccess();

        return response.TopicArn;
    }

    private async Task<string> CreateQueueIfNotExistsAsync(string queueName, EventRegistration reg, EventConsumerRegistration? ecr, CancellationToken cancellationToken)
    {
        // check if the queue exists
        var urlResponse = await sqsClient.Value.GetQueueUrlAsync(queueName: queueName, cancellationToken).ConfigureAwait(false);
        if (urlResponse != null && urlResponse.Successful()) return urlResponse.QueueUrl;

        // if entity creation is not enabled, throw exception
        if (!Options.EnableEntityCreation)
        {
            throw new InvalidOperationException("Entity creation is disabled. Required queue could not be created.");
        }

        // create the queue
        var request = new CreateQueueRequest(queueName: queueName);
        Options.SetupCreateQueueRequest?.Invoke(reg, ecr, request);
        var response = await sqsClient.Value.CreateQueueAsync(request: request, cancellationToken: cancellationToken).ConfigureAwait(false);
        response.EnsureSuccess();

        return response.QueueUrl;
    }

    private async Task ReceiveAsync(EventRegistration reg, EventConsumerRegistration ecr, string queueUrl, CancellationToken cancellationToken)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags) ?? throw new InvalidOperationException("Methods should be null");
        var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.Value.ReceiveMessageAsync(queueUrl, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccess();
                var messages = response.Messages;

                // if the response is empty, introduce a delay
                if (messages.Count == 0)
                {
                    var delay = Options.EmptyResultsDelay;
                    Logger.NoMessages(queueUrl: queueUrl, delay: delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Logger.ReceivedMessages(messagesCount: messages.Count, queueUrl: queueUrl);
                    using var scope = CreateScope(); // shared
                    foreach (var message in messages)
                    {
                        await ((Task)method.Invoke(this, new object[] { reg, ecr, queueUrl, message, cancellationToken, })!).ConfigureAwait(false);
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

    private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                 EventConsumerRegistration ecr,
                                                                 string queueUrl,
                                                                 Message message,
                                                                 CancellationToken cancellationToken)
        where TEvent : class
        where TConsumer : IEventConsumer
    {
        var messageId = message.MessageId;
        message.TryGetAttribute(MetadataNames.CorrelationId, out var correlationId);
        message.TryGetAttribute(MetadataNames.SequenceNumber, out var sequenceNumber);
        message.TryGetAttribute(MetadataNames.ActivityId, out var parentActivityId);

        using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                          correlationId: correlationId,
                                                          sequenceNumber: sequenceNumber,
                                                          extras: new Dictionary<string, string?>
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
        activity?.AddTag(ActivityTagNames.MessagingDestination, reg.EventName);
        activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue");
        activity?.AddTag(ActivityTagNames.MessagingUrl, queueUrl);

        Logger.ProcessingMessage(messageId: messageId, queueUrl: queueUrl);
        message.TryGetAttribute("Content-Type", out var contentType_str);
        var contentType = contentType_str == null ? null : new ContentType(contentType_str);

        using var scope = CreateScope();
        var context = await DeserializeAsync<TEvent>(scope: scope,
                                                     body: new BinaryData(message.Body),
                                                     contentType: contentType,
                                                     registration: reg,
                                                     identifier: messageId,
                                                     raw: message,
                                                     deadletter: ecr.Deadletter,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);

        Logger.ReceivedMessage(messageId: messageId, eventBusId: context.Id, queueUrl: queueUrl);

        var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(registration: reg,
                                                                    ecr: ecr,
                                                                    @event: context,
                                                                    scope: scope,
                                                                    cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!successful && ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
        {
            // get the queueUrl for the dead letter queue and send the message there
            var dlqQueueUrl = await GetQueueUrlAsync(reg: reg, ecr: ecr, deadletter: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            var dlqRequest = new SendMessageRequest
            {
                MessageAttributes = message.MessageAttributes,
                MessageBody = message.Body,
                QueueUrl = dlqQueueUrl,
            };
            await sqsClient.Value.SendMessageAsync(request: dlqRequest, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // whether or not successful, always delete the message from the current queue
        Logger.DeletingMessage(messageId: messageId, queueUrl: queueUrl);
        await sqsClient.Value.DeleteMessageAsync(queueUrl: queueUrl,
                                                 receiptHandle: message.ReceiptHandle,
                                                 cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    ///
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                stoppingCts.Cancel();
            }

            disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
