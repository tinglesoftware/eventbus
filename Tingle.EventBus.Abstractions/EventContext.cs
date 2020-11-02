using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
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
        Task<string> IEventBusPublisher.PublishAsync<TEvent>(TEvent @event, DateTimeOffset? scheduled, CancellationToken cancellationToken)
        {
            var ctx = new EventContext<TEvent>
            {
                CorrelationId = EventId,
            };

            return bus.PublishAsync(ctx, scheduled, cancellationToken);
        }

        internal void SetBus(IEventBus bus)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }
    }

    public class EventContext<T> : EventContext
    {
        internal EventContext()
        {

        }

        internal EventContext(T @event) : this()
        {
            Event = @event;
        }

        /// <summary>
        /// The event published
        /// </summary>
        public T Event { get; set; }
    }
}
