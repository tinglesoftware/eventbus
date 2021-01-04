using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    /// <summary>
    /// Extension methods for <see cref="IEventPublisher"/>
    /// </summary>
    public static class IEventPublisherExtensions
    {
        /// <summary>
        /// Publish an event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="event">The event to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<string> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                        EventContext<TEvent> @event,
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
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="events">The events to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<IList<string>> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                               IList<EventContext<TEvent>> events,
                                                               TimeSpan delay,
                                                               CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = DateTimeOffset.UtcNow + delay;
            return publisher.PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Publish an event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="event">The event to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<string> PublishAsync<TEvent>(this IEventPublisher publisher,
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
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="events">The events to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<IList<string>> PublishAsync<TEvent>(this IEventPublisher publisher,
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
