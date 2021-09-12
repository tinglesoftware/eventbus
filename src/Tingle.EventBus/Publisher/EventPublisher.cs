using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    internal class EventPublisher : IEventPublisher
    {
        private readonly EventBus bus;

        public EventPublisher(EventBus bus)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <inheritdoc/>
        public EventContext<TEvent> CreateEventContext<TEvent>(TEvent @event, string? correlationId = null)
            where TEvent : class
        {
            return new EventContext<TEvent>(this, @event);
        }

        /// <inheritdoc/>
        public async Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                 DateTimeOffset? scheduled = null,
                                                                 CancellationToken cancellationToken = default)
            where TEvent : class
        {
            return await bus.PublishAsync(@event, scheduled, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                        DateTimeOffset? scheduled = null,
                                                                        CancellationToken cancellationToken = default)
            where TEvent : class
        {
            return await bus.PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default) where TEvent : class
        {
            await bus.CancelAsync<TEvent>(id: id, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default) where TEvent : class
        {
            await bus.CancelAsync<TEvent>(ids: ids, cancellationToken: cancellationToken);
        }
    }
}
