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
    public class InMemoryTransport : EventBusTransportBase<InMemoryTransportOptions>, IDisposable
    {
        private readonly Dictionary<(Type, bool), InMemoryQueueEntity> queuesCache = new();
        private readonly SemaphoreSlim queuesCacheLock = new(1, 1); // only one at a time.
        private readonly CancellationTokenSource stoppingCts = new();
        private readonly List<Task> receiverTasks = new();

        private readonly ConcurrentBag<EventContext> published = new();
        private readonly ConcurrentBag<EventContext> consumed = new();
        private readonly ConcurrentBag<EventContext> failed = new();

        private readonly SequenceNumberGenerator sng;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="sng"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public InMemoryTransport(IServiceScopeFactory serviceScopeFactory,
                                 SequenceNumberGenerator sng,
                                 IOptions<EventBusOptions> busOptionsAccessor,
                                 IOptions<InMemoryTransportOptions> transportOptionsAccessor,
                                 ILoggerFactory loggerFactory)
            : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            this.sng = sng;
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

            if (receiverTasks.Count > 0)
            {
                throw new InvalidOperationException("The bus has already been started.");
            }

            var registrations = GetRegistrations();
            foreach (var reg in registrations)
            {
                foreach (var ecr in reg.Consumers)
                {
                    var t = ReceiveAsync(reg: reg, ecr: ecr, cancellationToken: stoppingCts.Token);
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

            var message = new InMemoryQueueMessage(body)
            {
                MessageId = @event.Id,
                ContentType = @event.ContentType?.ToString(),
                CorrelationId = @event.CorrelationId,
            };

            // Add custom properties
            message.Properties.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                              .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                              .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

            // Add to published list
            published.Add(@event);

            // Get the queue and send the message accordingly
            var queueEntity = await GetQueueAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
            Logger.LogInformation("Sending {Id} to '{QueueName}'. Scheduled: {Scheduled}",
                                  @event.Id,
                                  queueEntity.Name,
                                  scheduled);
            if (scheduled != null)
            {
                _ = DelayThenExecuteAsync(scheduled.Value, (msg, ct) => queueEntity.EnqueueAsync(msg, ct), message);
                return new ScheduledResult(id: sng.Generate(), scheduled: scheduled.Value);
            }
            else
            {
                await queueEntity.EnqueueAsync(message);
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
            var messages = new List<InMemoryQueueMessage>();

            foreach (var @event in events)
            {
                var body = await SerializeAsync(scope: scope,
                                                @event: @event,
                                                registration: registration,
                                                cancellationToken: cancellationToken);

                var message = new InMemoryQueueMessage(body)
                {
                    MessageId = @event.Id,
                    CorrelationId = @event.CorrelationId,
                    ContentType = @event.ContentType?.ToString(),
                };

                // Add custom properties
                message.Properties.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                                  .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                                  .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

                messages.Add(message);
            }

            // Add to published list
            published.AddBatch(events);

            // Get the queue and send the message accordingly
            var queueEntity = await GetQueueAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
            Logger.LogInformation("Sending {EventsCount} messages to '{EntityPath}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                                  events.Count,
                                  queueEntity.Name,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.Id)));
            if (scheduled != null)
            {
                _ = DelayThenExecuteAsync(scheduled.Value, (msgs, ct) => queueEntity.EnqueueBatchAsync(msgs, ct), messages);
                return events.Select(_ => new ScheduledResult(id: sng.Generate(), scheduled: scheduled.Value)).ToList();
            }
            else
            {
                await queueEntity.EnqueueBatchAsync(messages);
                return Array.Empty<ScheduledResult>(); // no sequence numbers available
            }
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("InMemory transport does not support canceling published messages.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("InMemory transport does not support canceling published messages.");
        }

        private async Task<InMemoryQueueEntity> GetQueueAsync(EventRegistration reg, bool deadletter, CancellationToken cancellationToken)
        {
            await queuesCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!queuesCache.TryGetValue((reg.EventType, deadletter), out var queue))
                {
                    var name = reg.EventName!;
                    if (deadletter) name += TransportOptions.DeadLetterSuffix;

                    queue = new InMemoryQueueEntity(name: name, TransportOptions.DeliveryDelay);

                    queuesCache[(reg.EventType, deadletter)] = queue;
                }

                return queue;
            }
            finally
            {
                queuesCacheLock.Release();
            }
        }

        private async Task ReceiveAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
            var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);

            var queueEntity = await GetQueueAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            var queueName = queueEntity.Name;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var message = await queueEntity.DequeueAsync(cancellationToken);
                    Logger.LogDebug("Received a message on '{QueueName}'", queueName);
                    using var scope = CreateScope(); // shared
                    await (Task)method.Invoke(this, new object[] { reg, ecr, queueEntity, message, scope, cancellationToken, });
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
                                                                     InMemoryQueueEntity queueEntity,
                                                                     InMemoryQueueMessage message,
                                                                     IServiceScope scope,
                                                                     CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventConsumer<TEvent>
        {
            var messageId = message.MessageId;

            message.Properties.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

            using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                              correlationId: null,
                                                              extras: new Dictionary<string, string?>
                                                              {
                                                                  ["QueueName"] = queueEntity.Name,
                                                              });

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
            activity?.AddTag(ActivityTagNames.MessagingDestination, queueEntity.Name);
            activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue");

            Logger.LogDebug("Processing '{MessageId}' from '{QueueName}'", messageId, queueEntity.Name);
            var contentType = new ContentType(message.ContentType);
            var context = await DeserializeAsync<TEvent>(scope: scope,
                                                         body: message.Body,
                                                         contentType: contentType,
                                                         registration: reg,
                                                         identifier: messageId,
                                                         cancellationToken: cancellationToken);

            Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}' from '{QueueName}'",
                                  messageId,
                                  context.Id,
                                  queueEntity.Name);

            // set the extras
            context.SetInMemoryMessage(message);

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

                // Deadletter if needed
                if (ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
                {
                    // get the dead letter queue and send the mesage there
                    var dlqEntity = await GetQueueAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                    await dlqEntity.EnqueueAsync(message);
                }
            }
        }

        private async Task DelayThenExecuteAsync<TArg>(DateTimeOffset scheduled, Func<TArg, CancellationToken, Task> action, TArg arg, CancellationToken cancellationToken = default)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var remainder = scheduled - DateTimeOffset.UtcNow;
            if (remainder > TimeSpan.Zero)
            {
                await Task.Delay(remainder, cancellationToken);
            }

            await action(arg, cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            stoppingCts.Cancel();
        }
    }
}
