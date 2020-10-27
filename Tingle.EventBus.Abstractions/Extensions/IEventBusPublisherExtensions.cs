using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public static class IEventBusPublisherExtensions
    {
        /// <summary>
        /// Publish an event to be consumed (or available for consumption) after a given duration.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventBusPublisher"/> to use.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="event">THe event to publish.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<string> PublishAsync<TEvent>(this IEventBusPublisher publisher, TimeSpan delay, TEvent @event, CancellationToken cancellationToken = default)
        {
            var scheduled = DateTimeOffset.UtcNow + delay;
            return publisher.PublishAsync<TEvent>(@event: @event, scheduled: scheduled, cancellationToken: cancellationToken);
        }
    }
}
