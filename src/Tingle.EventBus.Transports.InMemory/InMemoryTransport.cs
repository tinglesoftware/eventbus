using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Mime;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Transports.InMemory.Client;

namespace Tingle.EventBus.Transports.InMemory;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using an in-memory transport.
/// This implementation should only be used for unit testing or similar scenarios as it does not offer persistence.
/// </summary>
public class InMemoryTransport : EventBusTransport<InMemoryTransportOptions>
{
    private readonly EventBusConcurrentDictionary<Type, InMemorySender> sendersCache = new();
    private readonly EventBusConcurrentDictionary<string, InMemoryProcessor> processorsCache = new();
    private readonly InMemoryClient inMemoryClient;

    private readonly ConcurrentBag<EventContext> published = new();
    private readonly ConcurrentBag<long> cancelled = new();
    private readonly ConcurrentBag<EventContext> consumed = new();
    private readonly ConcurrentBag<EventContext> failed = new();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="sng"></param>
    public InMemoryTransport(IServiceScopeFactory serviceScopeFactory,
                             IOptions<EventBusOptions> busOptionsAccessor,
                             IOptionsMonitor<InMemoryTransportOptions> optionsMonitor,
                             ILoggerFactory loggerFactory,
                             SequenceNumberGenerator sng)
        : base(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory)
    {
        inMemoryClient = new InMemoryClient(sng);
    }

    /// <summary>
    /// The published events.
    /// </summary>
    internal ConcurrentBag<EventContext> Published => published;

    /// <summary>
    /// The cancelled events.
    /// </summary>
    internal ConcurrentBag<long> Cancelled => cancelled;

    /// <summary>
    /// The consumed events.
    /// </summary>
    internal ConcurrentBag<EventContext> Consumed => consumed;

    /// <summary>
    /// The failed events.
    /// </summary>
    internal ConcurrentBag<EventContext> Failed => failed;

    /// <inheritdoc/>
    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            foreach (var ecr in reg.Consumers.Values)
            {
                var processor = await GetProcessorAsync(reg: reg, ecr: ecr, cancellationToken: cancellationToken).ConfigureAwait(false);

                // register handlers for error and processing
                processor.ProcessErrorAsync += OnMessageFaultedAsync;
                processor.ProcessMessageAsync += delegate (ProcessMessageEventArgs args)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags) ?? throw new InvalidOperationException("Methods should be null");
                    var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);
                    return (Task)method.Invoke(this, new object[] { reg, ecr, processor, args, })!;
                };

                // start processing
                Logger.StartingProcessing(entityPath: processor.EntityPath);
                await processor.StartProcessingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var clients = processorsCache.ToArray().Select(kvp => (key: kvp.Key, proc: kvp.Value)).ToList();
        foreach (var (key, t) in clients)
        {
            Logger.StoppingProcessor(processor: key);

            try
            {
                var proc = await t.ConfigureAwait(false);
                await proc.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
                processorsCache.TryRemove(key, out _);

                Logger.StoppedProcessor(processor: key);
            }
            catch (Exception exception)
            {
                Logger.StopProcessorFaulted(processor: key, ex: exception);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task<ScheduledResult?> PublishCoreAsync<TEvent>(EventContext<TEvent> @event,
                                                                             EventRegistration registration,
                                                                             DateTimeOffset? scheduled = null,
                                                                             CancellationToken cancellationToken = default)
    {
        // log warning when trying to publish scheduled message
        if (scheduled != null)
        {
            Logger.SchedulingShortLived();
        }

        using var scope = CreateScope();
        var body = await SerializeAsync(scope: scope,
                                        @event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

        var message = new InMemoryMessage(body)
        {
            MessageId = @event.Id,
            ContentType = @event.ContentType?.ToString(),
            CorrelationId = @event.CorrelationId,
        };

        // If scheduled for later, set the value in the message
        if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
        {
            message.Scheduled = scheduled.Value.UtcDateTime;
        }

        // Add custom properties
        message.Properties.ToEventBusWrapper()
                          .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                          .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                          .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

        // Add to published list
        published.Add(@event);

        // Get the queue and send the message accordingly
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.SendingMessage(eventBusId: @event.Id, entityPath: sender.EntityPath, scheduled: scheduled);
        if (scheduled != null)
        {
            var seqNum = await sender.ScheduleMessageAsync(message: message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new ScheduledResult(id: seqNum, scheduled: scheduled.Value); // return the sequence number
        }
        else
        {
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            return null; // no sequence number available
        }
    }

    /// <inheritdoc/>
    protected async override Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                    EventRegistration registration,
                                                                                    DateTimeOffset? scheduled = null,
                                                                                    CancellationToken cancellationToken = default)
    {
        // log warning when trying to publish scheduled message
        if (scheduled != null)
        {
            Logger.SchedulingShortLived();
        }

        using var scope = CreateScope();
        var messages = new List<InMemoryMessage>();

        foreach (var @event in events)
        {
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);

            var message = new InMemoryMessage(body)
            {
                MessageId = @event.Id,
                CorrelationId = @event.CorrelationId,
                ContentType = @event.ContentType?.ToString(),
            };

            // If scheduled for later, set the value in the message
            if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
            {
                message.Scheduled = scheduled.Value.UtcDateTime;
            }

            // Add custom properties
            message.Properties.ToEventBusWrapper()
                              .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                              .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                              .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

            messages.Add(message);
        }

        // Add to published list
        AddBatch(published, events);

        // Get the queue and send the message accordingly
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.SendingMessages(events: events, entityPath: sender.EntityPath, scheduled: scheduled);
        if (scheduled != null)
        {
            var seqNums = await sender.ScheduleMessagesAsync(messages: messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            return seqNums.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList(); // return the sequence numbers
        }
        else
        {
            await sender.SendMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
            return Array.Empty<ScheduledResult>(); // no sequence numbers available
        }
    }

    /// <inheritdoc/>
    protected override async Task CancelCoreAsync<TEvent>(string id,
                                                          EventRegistration registration,
                                                          CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace", nameof(id));
        }

        if (!long.TryParse(id, out var seqNum))
        {
            throw new ArgumentException($"'{nameof(id)}' is malformed or invalid", nameof(id));
        }

        // get the entity and cancel the message accordingly
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.CancelingMessage(sequenceNumber: seqNum, entityPath: sender.EntityPath);
        await sender.CancelScheduledMessageAsync(sequenceNumber: seqNum, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Add to cancelled list
        cancelled.Add(seqNum);
    }

    /// <inheritdoc/>
    protected override async Task CancelCoreAsync<TEvent>(IList<string> ids,
                                                          EventRegistration registration,
                                                          CancellationToken cancellationToken = default)
    {
        if (ids is null) throw new ArgumentNullException(nameof(ids));

        var seqNums = ids.Select(i =>
        {
            if (!long.TryParse(i, out var seqNum))
            {
                throw new ArgumentException($"'{nameof(i)}' is malformed or invalid", nameof(i));
            }
            return seqNum;
        }).ToList();

        // get the entity and cancel the message accordingly
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.CancelingMessages(sequenceNumbers: seqNums, entityPath: sender.EntityPath);
        await sender.CancelScheduledMessagesAsync(sequenceNumbers: seqNums, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Add to cancelled list
        AddBatch(cancelled, seqNums);
    }

    private Task<InMemorySender> GetSenderAsync(EventRegistration reg, CancellationToken cancellationToken)
    {
        Task<InMemorySender> creator(Type _, CancellationToken ct)
        {
            var sender = inMemoryClient.CreateSender(name: reg.EventName!, broadcast: reg.EntityKind == EntityKind.Broadcast);
            return Task.FromResult(sender);
        }
        return sendersCache.GetOrAddAsync(reg.EventType, creator, cancellationToken);
    }

    private Task<InMemoryProcessor> GetProcessorAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
    {
        var topicName = reg.EventName!;
        var subscriptionName = ecr.ConsumerName!;

        var key = $"{topicName}/{subscriptionName}";
        Task<InMemoryProcessor> creator(string _, CancellationToken ct)
        {
            // Create the processor options
            var inpo = new InMemoryProcessorOptions { };

            // Create the processor.
            InMemoryProcessor processor;
            if (reg.EntityKind == EntityKind.Queue)
            {
                // Create the processor for the Queue
                Logger.CreatingQueueProcessor(queueName: topicName);
                processor = inMemoryClient.CreateProcessor(queueName: topicName, options: inpo);
            }
            else
            {
                // Create the processor for the Subscription
                Logger.CreatingSubscriptionProcessor(topicName: topicName, subscriptionName: subscriptionName);
                processor = inMemoryClient.CreateProcessor(topicName: topicName, subscriptionName: subscriptionName, options: inpo);
            }

            return Task.FromResult(processor);
        }
        return processorsCache.GetOrAddAsync(key, creator, cancellationToken);
    }

    private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                 EventConsumerRegistration ecr,
                                                                 InMemoryProcessor processor,
                                                                 ProcessMessageEventArgs args)
        where TEvent : class
        where TConsumer : IEventConsumer
    {
        var entityPath = processor.EntityPath;
        var message = args.Message;
        var messageId = message.MessageId;
        var cancellationToken = args.CancellationToken;

        message.Properties.TryGetValue(MetadataNames.ActivityId, out var parentActivityId);

        using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                          correlationId: message.CorrelationId,
                                                          sequenceNumber: message.SequenceNumber.ToString(),
                                                          extras: new Dictionary<string, string?>
                                                          {
                                                              ["EntityPath"] = entityPath,
                                                          });

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
        activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
        var destination = reg.EntityKind == EntityKind.Queue ? reg.EventName : ecr.ConsumerName;
        activity?.AddTag(ActivityTagNames.MessagingDestination, destination); // name of the queue/subscription
        activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue"); // the spec does not know subscription so we can only use queue for both

        Logger.ProcessingMessage(messageId: messageId, entityPath: entityPath);
        using var scope = CreateScope();
        var contentType = message.ContentType is not null ? new ContentType(message.ContentType) : null;
        var context = await DeserializeAsync<TEvent>(scope: scope,
                                                     body: message.Body,
                                                     contentType: contentType,
                                                     registration: reg,
                                                     identifier: message.SequenceNumber.ToString(),
                                                     raw: message,
                                                     deadletter: ecr.Deadletter,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);

        Logger.ReceivedMessage(sequenceNumber: message.SequenceNumber, eventBusId: context.Id, entityPath: entityPath);

        // set the extras
        context.SetInMemoryReceivedMessage(message);

        var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(registration: reg,
                                                                    ecr: ecr,
                                                                    @event: context,
                                                                    scope: scope,
                                                                    cancellationToken: cancellationToken).ConfigureAwait(false);

        if (successful)
        {
            // Add to Consumed list
            consumed.Add(context);
        }
        else
        {
            // Add to failed list
            failed.Add(context);
        }
    }

    private Task OnMessageFaultedAsync(ProcessErrorEventArgs args)
    {
        Logger.MessageReceivingFaulted(entityPath: args.EntityPath,
                                       errorSource: args.ErrorSource,
                                       ex: args.Exception);
        return Task.CompletedTask;
    }

    internal static void AddBatch<T>(ConcurrentBag<T> bag, IEnumerable<T> items)
    {
        if (bag is null) throw new ArgumentNullException(nameof(bag));
        if (items is null) throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            bag.Add(item);
        }
    }
}
