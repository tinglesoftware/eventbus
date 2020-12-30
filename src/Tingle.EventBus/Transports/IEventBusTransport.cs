using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports
{
    /// <summary>
    /// Contract for implementing an event bus transport.
    /// </summary>
    public interface IEventBusTransport
    {
        /// <summary>
        /// Checks for health of the transport.
        /// This function can be used by the Health Checks framework and may throw and execption during execution.
        /// </summary>
        /// <param name="extras">The extra information about the health check.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A value indicating if the bus is healthly.</returns>
        Task<bool> CheckHealthAsync(EventBusHealthCheckExtras extras,
                                    CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish an event on the transport.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                          DateTimeOffset? scheduled = null,
                                          CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <summary>
        /// Publish a batch of events on the transport.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="events">The events to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                 DateTimeOffset? scheduled = null,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <summary>
        /// Cancel a scheduled event on the transport.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="id">The identifier of the scheduled event.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <summary>
        /// Cancel a batch of scheduled events on the transport.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="ids">The identifiers of the scheduled events.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <summary>
        /// Triggered when the bus host is ready to start.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns></returns>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Triggered when the bus host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        /// <returns></returns>
        Task StopAsync(CancellationToken cancellationToken);
    }
}
