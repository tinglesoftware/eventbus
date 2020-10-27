using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public interface IEventBusPublisher
    {
        /// <summary>
        /// Publish an event to be consumed immediately or sometime in the future.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> PublishAsync<TEvent>(TEvent @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default);

        // <summary>
        /// Publish an event to be consumed (or available for consumption) after a given duration.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="event">THe event to publish.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<string> PublishAsync<TEvent>(TimeSpan delay, TEvent @event, CancellationToken cancellationToken = default)
        {
            var scheduled = DateTimeOffset.UtcNow + delay;
            return PublishAsync<TEvent>(@event: @event, scheduled: scheduled, cancellationToken: cancellationToken);
        }
    }
}
