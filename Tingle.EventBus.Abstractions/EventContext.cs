using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public abstract class EventContext : IEventBusPublisher
    {
        private IEventBus bus;

        /// <summary>
        /// The headers published alongside the event.
        /// </summary>
        public EventHeaders Headers { get; set; }

        /// <inheritdoc/>
        Task<string> IEventBusPublisher.PublishAsync<TEvent>(TEvent @event, DateTimeOffset? scheduled, CancellationToken cancellationToken)
        {
            var ctx = new EventContext<TEvent>
            {
                Headers = new EventHeaders
                {
                    CorrelationId = Headers.MessageId,
                }
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
        /// <summary>
        /// The event published
        /// </summary>
        public T Event { get; set; }
    }
}
