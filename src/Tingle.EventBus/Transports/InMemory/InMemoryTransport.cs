using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.InMemory
{
#pragma warning disable CA1063 // Implement IDisposable Correctly
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using an in-memory transport.
    /// This implementation should only be used for unit testing or similar scenarios as it does not offer persistence.
    /// </summary>
    [TransportName(TransportNames.InMemory)]
    public class InMemoryTransport : EventBusTransportBase<InMemoryTransportOptions>, IDisposable
    {
        private readonly Dictionary<(Type, bool), InMemoryQueueEntity> queuesCache = new Dictionary<(Type, bool), InMemoryQueueEntity>();
        private readonly SemaphoreSlim queuesCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly CancellationTokenSource stoppingCts = new CancellationTokenSource();
        private readonly List<Task> receiverTasks = new List<Task>();

        private readonly ConcurrentBag<object> published = new ConcurrentBag<object>();
        private readonly ConcurrentBag<object> consumed = new ConcurrentBag<object>();
        private readonly ConcurrentBag<object> failed = new ConcurrentBag<object>();

        private readonly SequenceNumberGenerator sng;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="sng"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public InMemoryTransport(IHostEnvironment environment,
                                 IServiceScopeFactory serviceScopeFactory,
                                 SequenceNumberGenerator sng,
                                 IOptions<EventBusOptions> busOptionsAccessor,
                                 IOptions<InMemoryTransportOptions> transportOptionsAccessor,
                                 ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            this.sng = sng;
        }

        /// <summary>
        /// The published events.
        /// </summary>
        public IEnumerable<object> Published => published;

        /// <summary>
        /// The consumed events.
        /// </summary>
        public IEnumerable<object> Consumed => consumed;

        /// <summary>
        /// The failed events.
        /// </summary>
        public IEnumerable<object> Failed => failed;

        /// <inheritdoc/>
        public override Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                    CancellationToken cancellationToken = default)
        {
            var registrations = GetConsumerRegistrations();
            Logger.StartingTransport(registrations.Count);
            foreach (var reg in registrations)
            {
                var t = ReceiveAsync(reg: reg, cancellationToken: stoppingCts.Token);
                receiverTasks.Add(t);
            }

            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = GetConsumerRegistrations();
            Logger.StartingTransport(registrations.Count);
            foreach (var reg in registrations)
            {
                var t = ReceiveAsync(reg: reg, cancellationToken: stoppingCts.Token);
                receiverTasks.Add(t);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.StoppingTransport();

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
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("InMemory EventBus uses a short-lived timer that is not persisted for scheduled publish");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   registration: reg,
                                                   scope: scope,
                                                   cancellationToken: cancellationToken);

            var message = new InMemoryQueueMessage(ms.ToArray())
            {
                MessageId = @event.Id,
                ContentType = contentType?.ToString(),
                CorrelationId = @event.CorrelationId,
            };

            // Add custom properties
            message.Properties.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                              .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                              .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

            // Add to published list
            published.Add(@event);

            // Get the queue and send the message accordingly
            var queueEntity = await GetQueueAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            var queue = queueEntity.Queue;
            Logger.LogInformation("Sending {Id} to '{QueueName}'. Scheduled: {Scheduled}",
                                  @event.Id,
                                  queueEntity.Name,
                                  scheduled);
            if (scheduled != null)
            {
                _ = DelayThenExecuteAsync(scheduled.Value, (msg, ct) =>
                {
                    queue.Enqueue(msg);
                    return Task.CompletedTask;
                }, message);
                return sng.Generate();
            }
            else
            {
                queue.Enqueue(message);
                return null; // no sequence number available
            }
        }

        /// <inheritdoc/>
        public async override Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
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
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();

            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       registration: reg,
                                                       scope: scope,
                                                       cancellationToken: cancellationToken);

                var message = new InMemoryQueueMessage(ms.ToArray())
                {
                    MessageId = @event.Id,
                    CorrelationId = @event.CorrelationId,
                    ContentType = contentType.ToString(),
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
            var queueEntity = await GetQueueAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            var queue = queueEntity.Queue;
            Logger.LogInformation("Sending {EventsCount} messages to '{EntityPath}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                                  events.Count,
                                  queueEntity.Name,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.Id)));
            if (scheduled != null)
            {
                _ = DelayThenExecuteAsync(scheduled.Value, (msgs, ct) =>
                {
                    queue.EnqueueBatch(msgs);
                    return Task.CompletedTask;
                }, messages);
                return events.Select(_ => sng.Generate()).ToList();
            }
            else
            {
                queue.EnqueueBatch(messages);
                return Array.Empty<string>(); // no sequence numbers available
            }
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("InMemory transport does not support canceling published messages.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
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
                    var name = reg.EventName;
                    if (deadletter) name += TransportOptions.DeadLetterSuffix;

                    queue = new InMemoryQueueEntity(name: name);

                    queuesCache[(reg.EventType, deadletter)] = queue;
                }

                return queue;
            }
            finally
            {
                queuesCacheLock.Release();
            }
        }

        private async Task ReceiveAsync(ConsumerRegistration reg, CancellationToken cancellationToken)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
            var method = mt.MakeGenericMethod(reg.EventType, reg.ConsumerType);

            var queueEntity = await GetQueueAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            var queue = queueEntity.Queue;
            var queueName = queueEntity.Name;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (queue.TryDequeue(out var message))
                    {
                        Logger.LogDebug("Received a message on '{QueueName}'", queueName);
                        using var scope = CreateScope(); // shared
                        await (Task)method.Invoke(this, new object[] { reg, queueEntity, message, scope, cancellationToken, });
                    }
                    else
                    {
                        // if there is nothing to dequeue, introduce a delay
                        var delay = TransportOptions.EmptyResultsDelay;
                        Logger.LogTrace("No messages on '{QueueName}', delaying check for {Delay}", queueName, delay);
                        await Task.Delay(delay, cancellationToken);
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

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg,
                                                                     InMemoryQueueEntity queueEntity,
                                                                     InMemoryQueueMessage message,
                                                                     IServiceScope scope,
                                                                     CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            var messageId = message.MessageId;

            message.Properties.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

            using var log_scope = Logger.BeginScopeForConsume(id: messageId, correlationId: null);

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
            activity?.AddTag(ActivityTagNames.MessagingDestination, queueEntity.Name);
            activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue");

            EventContext<TEvent> context=null;
            try
            {
                Logger.LogDebug("Processing '{MessageId}'", messageId);
                using var ms = new MemoryStream(message.Body.ToArray());
                var contentType = new ContentType(message.ContentType);
                context = await DeserializeAsync<TEvent>(body: ms,
                                                         contentType: contentType,
                                                         registration: reg,
                                                         scope: scope,
                                                         cancellationToken: cancellationToken);

                Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}'",
                                      messageId,
                                      context.Id);

                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);

                // Add to Consumed list
                consumed.Add(context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event processing failed. Moving to deadletter.");

                // Add to failed list
                failed.Add(context);

                // get the dead letter queue and send the mesage there
                var dlqEntity = await GetQueueAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                dlqEntity.Queue.Enqueue(message);
            }
        }

        private async Task DelayThenExecuteAsync<TArg>(DateTimeOffset scheduled, Func<TArg, CancellationToken, Task> action, TArg arg, CancellationToken cancellationToken = default)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var remainder = DateTimeOffset.UtcNow - scheduled;
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
#pragma warning restore CA1063 // Implement IDisposable Correctly
    }
}
