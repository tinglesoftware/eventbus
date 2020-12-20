using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    /// <summary>
    /// Extension methods for <see cref="IEventBusPublisher"/>
    /// </summary>
    public static class IEventBusPublisherExtensions
    {
        /// <summary>
        /// Publish an event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventBusPublisher"/> to extend.</param>
        /// <param name="event">The event to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<string> PublishAsync<TEvent>(this IEventBusPublisher publisher,
                                                        TEvent @event,
                                                        TimeSpan delay,
                                                        CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = DateTimeOffset.UtcNow + delay;
            return publisher.PublishAsync(@event: @event, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Publish a batch of events.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventBusPublisher"/> to extend.</param>
        /// <param name="events">The events to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<IList<string>> PublishAsync<TEvent>(this IEventBusPublisher publisher,
                                                               IList<TEvent> events,
                                                               TimeSpan delay,
                                                               CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = DateTimeOffset.UtcNow + delay;
            return publisher.PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken);
        }
    }
}
