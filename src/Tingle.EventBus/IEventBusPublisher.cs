using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    /// <summary>
    /// Contract describing a publisher of events.
    /// </summary>
    public interface IEventBusPublisher
    {
        /// <summary>
        /// Publish an event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> PublishAsync<TEvent>(TEvent @event,
                                          DateTimeOffset? scheduled = null,
                                          CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <summary>
        /// Publish an event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<string> PublishAsync<TEvent>(TEvent @event,
                                                 TimeSpan delay,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = DateTimeOffset.UtcNow + delay;
            return PublishAsync(@event: @event, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Publish a batch of events.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="events">The events to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IList<string>> PublishAsync<TEvent>(IList<TEvent> events,
                                                 DateTimeOffset? scheduled = null,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <summary>
        /// Publish a batch of events.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="events">The events to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<IList<string>> PublishAsync<TEvent>(IList<TEvent> events,
                                                        TimeSpan delay,
                                                        CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = DateTimeOffset.UtcNow + delay;
            return PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken);
        }
    }
}
