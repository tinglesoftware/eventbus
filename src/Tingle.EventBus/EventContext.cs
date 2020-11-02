using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    public abstract class EventContext : IEventBusPublisher
    {
        private IEventBus bus;

        public string EventId { get; set; }
        public string RequestId { get; set; }
        public string CorrelationId { get; set; }
        public string ConversationId { get; set; }
        public string InitiatorId { get; set; }
        public DateTimeOffset? Expires { get; set; }
        public DateTimeOffset? Sent { get; set; }

        /// <summary>
        /// The headers published alongside the event.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

        /// <inheritdoc/>
        Task<string> IEventBusPublisher.PublishAsync<TEvent>(TEvent @event,
                                                             DateTimeOffset? scheduled,
                                                             CancellationToken cancellationToken)
        {
            var context = new EventContext<TEvent>
            {
                CorrelationId = EventId,
                Event = @event,
            };

            return bus.PublishAsync(@event: context, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        Task<IList<string>> IEventBusPublisher.PublishAsync<TEvent>(IList<TEvent> events,
                                                                    DateTimeOffset? scheduled,
                                                                    CancellationToken cancellationToken)
        {
            var contexts = events.Select(e => new EventContext<TEvent>
            {
                CorrelationId = EventId,
                Event = e,
            }).ToList();
            return bus.PublishAsync(events: contexts, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        internal void SetBus(IEventBus bus)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }
    }

    public class EventContext<T> : EventContext
    {
        /// <summary>
        /// The event published
        /// </summary>
        public T Event { get; set; }
    }
}
