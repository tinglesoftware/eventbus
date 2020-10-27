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
        Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default);
    }
}
