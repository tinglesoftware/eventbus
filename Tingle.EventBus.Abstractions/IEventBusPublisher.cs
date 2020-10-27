using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public interface IEventBusPublisher
    {
        /// <summary>
        /// Publish an event on the bus.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">THe event to publish.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish an event to be consumed (or available for consumption) at a given time in the future.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="time">The time at which the event should be availed for cunsumption</param>
        /// <param name="event">THe event to publish.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> SchedulePublishAsync<TEvent>(DateTimeOffset time, TEvent @event, CancellationToken cancellationToken = default);
    }
}
