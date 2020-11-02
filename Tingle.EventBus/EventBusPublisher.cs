using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    internal class EventBusPublisher : IEventBusPublisher
    {
        private readonly IEventBus eventBus;

        public EventBusPublisher(IEventBus eventBus)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc/>
        public async Task<string> PublishAsync<TEvent>(TEvent @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var context = new EventContext<TEvent>
            {
                Event = @event
            };

            return await eventBus.PublishAsync(context, scheduled, cancellationToken);
        }
    }
}
