using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Tingle.EventBus.Abstractions
{
    public interface IEventBus : IHostedService
    {
        /// <summary>
        /// Checks for health of the bus.
        /// This function can be used by the Health Checks framework and may throw and execption during execution.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish an event on the bus.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">THe event to publish.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task PublishAsync<TEvent>(EventContext<TEvent> @event, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish an event to be consumed (or available for consumption) at a given time in the future.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="time">The time at which the event should be availed for cunsumption</param>
        /// <param name="event">THe event to publish.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> SchedulePublishAsync<TEvent>(DateTimeOffset time, EventContext<TEvent> @event, CancellationToken cancellationToken = default);
    }
}
