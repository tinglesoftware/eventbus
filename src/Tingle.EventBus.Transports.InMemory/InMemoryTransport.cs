using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.InMemory
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using an in-memory transport.
    /// This implementation should only be used for unit testing or similar scenarios as it does not offer persistence.
    /// </summary>
    [TransportName(TransportNames.InMemory)]
    public class InMemoryTransport : EventBusTransportBase<InMemoryTransportOptions>
    {
        private readonly Dictionary<Type, InMemorySender> sendersCache = new();
        private readonly SemaphoreSlim sendersCacheLock = new(1, 1); // only one at a time.
        private readonly Dictionary<string, InMemoryProcessor> processorsCache = new();
        private readonly SemaphoreSlim processorsCacheLock = new(1, 1); // only one at a time.
        private readonly InMemoryClient inMemoryClient;

        private readonly ConcurrentBag<EventContext> published = new();
        private readonly ConcurrentBag<EventContext> consumed = new();
        private readonly ConcurrentBag<EventContext> failed = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="sng"></param>
        public InMemoryTransport(IServiceScopeFactory serviceScopeFactory,
                                 IOptions<EventBusOptions> busOptionsAccessor,
                                 IOptions<InMemoryTransportOptions> transportOptionsAccessor,
                                 ILoggerFactory loggerFactory,
                                 SequenceNumberGenerator sng)
            : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            inMemoryClient = new InMemoryClient(sng);
        }

        /// <summary>
        /// The published events.
        /// </summary>
        internal ConcurrentBag<EventContext> Published => published;

        /// <summary>
        /// The consumed events.
        /// </summary>
        internal ConcurrentBag<EventContext> Consumed => consumed;

        /// <summary>
        /// The failed events.
        /// </summary>
        internal ConcurrentBag<EventContext> Failed => failed;

        /// <inheritdoc/>
        public override Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                    CancellationToken cancellationToken = default)
        {
            // InMemory is always healthy
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            var registrations = GetRegistrations();
            foreach (var reg in registrations)
            {
                foreach (var ecr in reg.Consumers)
                {
                    var processor = await GetProcessorAsync(reg: reg, ecr: ecr, cancellationToken: cancellationToken);

                    // register handlers for error and processing
                    processor.ProcessMessageAsync += delegate (ProcessMessageEventArgs args)
                    {
                        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
                        var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);
                        return (Task)method.Invoke(this, new object[] { reg, ecr, processor, args, });
                    };

                    // start processing
                    Logger.LogInformation("Starting processing on {EntityPath}", processor.EntityPath);
                    await processor.StartProcessingAsync(cancellationToken: cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            var clients = processorsCache.Select(kvp => (key: kvp.Key, proc: kvp.Value)).ToList();
            foreach (var (key, proc) in clients)
            {
                Logger.LogDebug("Stopping client: {Processor}", key);

                try
                {
                    await proc.StopProcessingAsync(cancellationToken);
                    processorsCache.Remove(key);

                    Logger.LogDebug("Stopped processor for {Processor}", key);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(exception, "Stop processor faulted for {Processor}", key);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                          EventRegistration registration,
                                                                          DateTimeOffset? scheduled = null,
                                                                          CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("InMemory EventBus uses a short-lived timer that is not persisted for scheduled publish");
            }

            using var scope = CreateScope();
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken);

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
            message.Properties.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                              .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                              .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

            // Add to published list
            published.Add(@event);

            // Get the queue and send the message accordingly
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Sending {Id} to '{EntityPath}'. Scheduled: {Scheduled}",
                                  @event.Id,
                                  sender.EntityPath,
                                  scheduled);
            if (scheduled != null)
            {
                var seqNum = await sender.ScheduleMessageAsync(message: message, cancellationToken: cancellationToken);
                return new ScheduledResult(id: seqNum, scheduled: scheduled.Value); // return the sequence number
            }
            else
            {
                await sender.SendMessageAsync(message);
                return null; // no sequence number available
            }
        }

        /// <inheritdoc/>
        public async override Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                 EventRegistration registration,
                                                                                 DateTimeOffset? scheduled = null,
                                                                                 CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("InMemory EventBus uses a short-lived timer that is not persisted for scheduled publish");
            }

            using var scope = CreateScope();
            var messages = new List<InMemoryMessage>();

            foreach (var @event in events)
            {
                var body = await SerializeAsync(scope: scope,
                                                @event: @event,
                                                registration: registration,
                                                cancellationToken: cancellationToken);

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
                message.Properties.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                                  .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                                  .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

                messages.Add(message);
            }

            // Add to published list
            published.AddBatch(events);

            // Get the queue and send the message accordingly
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Sending {EventsCount} messages to '{EntityPath}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                                  events.Count,
                                  sender.EntityPath,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.Id)));
            if (scheduled != null)
            {
                var seqNums = await sender.ScheduleMessagesAsync(messages: messages, cancellationToken: cancellationToken);
                return seqNums.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList(); // return the sequence numbers
            }
            else
            {
                await sender.SendMessagesAsync(messages);
                return Array.Empty<ScheduledResult>(); // no sequence numbers available
            }
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(string id,
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
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Canceling scheduled message: {SequenceNumber} on {EntityPath}", seqNum, sender.EntityPath);
            await sender.CancelScheduledMessageAsync(sequenceNumber: seqNum, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(IList<string> ids,
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
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Canceling {EventsCount} scheduled messages on {EntityPath}:\r\n- {SequenceNumbers}",
                                  ids.Count,
                                  sender.EntityPath,
                                  string.Join("\r\n- ", seqNums));
            await sender.CancelScheduledMessagesAsync(sequenceNumbers: seqNums, cancellationToken: cancellationToken);
        }

        private async Task<InMemorySender> GetSenderAsync(EventRegistration reg, CancellationToken cancellationToken)
        {
            await sendersCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!sendersCache.TryGetValue(reg.EventType, out var sender))
                {
                    // Create the sender
                    var name = reg.EventName!;
                    sender = inMemoryClient.CreateSender(name: name, broadcast: reg.EntityKind == EntityKind.Broadcast);
                    sendersCache[reg.EventType] = sender;
                }

                return sender;
            }
            finally
            {
                sendersCacheLock.Release();
            }
        }

        private async Task<InMemoryProcessor> GetProcessorAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
        {
            await processorsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var topicName = reg.EventName!;
                var subscriptionName = ecr.ConsumerName!;

                var key = $"{topicName}/{subscriptionName}";
                if (!processorsCache.TryGetValue(key, out var processor))
                {
                    // Create the processor options
                    var inpo = new InMemoryProcessorOptions { };

                    // Create the processor.
                    if (reg.EntityKind == EntityKind.Queue)
                    {
                        // Create the processor for the Queue
                        Logger.LogDebug("Creating processor for queue '{QueueName}'", topicName);
                        processor = inMemoryClient.CreateProcessor(queueName: topicName, options: inpo);
                    }
                    else
                    {
                        // Create the processor for the Subscription
                        Logger.LogDebug("Creating processor for topic '{TopicName}' and subscription '{Subscription}'",
                                        topicName,
                                        subscriptionName);
                        processor = inMemoryClient.CreateProcessor(topicName: topicName,
                                                                   subscriptionName: subscriptionName,
                                                                   options: inpo);
                    }

                    processorsCache[key] = processor;
                }

                return processor;
            }
            finally
            {
                processorsCacheLock.Release();
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                     EventConsumerRegistration ecr,
                                                                     InMemoryProcessor processor,
                                                                     ProcessMessageEventArgs args)
            where TEvent : class
            where TConsumer : IEventConsumer<TEvent>
        {
            var entityPath = processor.EntityPath;
            var message = args.Message;
            var messageId = message.MessageId;
            var cancellationToken = args.CancellationToken;

            message.Properties.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

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

            Logger.LogDebug("Processing '{MessageId}' from '{EntityPath}'", messageId, entityPath);
            using var scope = CreateScope();
            var contentType = new ContentType(message.ContentType);
            var context = await DeserializeAsync<TEvent>(scope: scope,
                                                         body: message.Body,
                                                         contentType: contentType,
                                                         registration: reg,
                                                         identifier: message.SequenceNumber.ToString(),
                                                         cancellationToken: cancellationToken);

            Logger.LogInformation("Received message: '{SequenceNumber}' containing Event '{Id}' from '{EntityPath}'",
                                  message.SequenceNumber,
                                  context.Id,
                                  entityPath);

            // set the extras
            context.SetInMemoryReceivedMessage(message);

            var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(ecr: ecr,
                                                                        @event: context,
                                                                        scope: scope,
                                                                        cancellationToken: cancellationToken);

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
    }
}
