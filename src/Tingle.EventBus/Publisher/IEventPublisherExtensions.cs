using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    /// <summary>
    /// Extension methods for <see cref="IEventPublisher"/>
    /// </summary>
    public static class IEventPublisherExtensions
    {
        #region Publish

        /// <summary>Publish an event.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="event">The event to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<ScheduledResult?> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                                  TEvent @event,
                                                                  DateTimeOffset? scheduled = null,
                                                                  CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var context = publisher.CreateEventContext(@event);
            return publisher.PublishAsync(context, scheduled, cancellationToken);
        }

        /// <summary>Publish a batch of events.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="events">The events to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<IList<ScheduledResult>?> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                                         IList<TEvent> events,
                                                                         DateTimeOffset? scheduled = null,
                                                                         CancellationToken cancellationToken = default) where TEvent : class
        {
            var contexts = events.Select(e => publisher.CreateEventContext(e)).ToList();
            return publisher.PublishAsync(events: contexts, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        #endregion

        #region Delays

        /// <summary>Publish an event.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="event">The event to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<ScheduledResult?> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                                  EventContext<TEvent> @event,
                                                                  TimeSpan? delay,
                                                                  CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = delay is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow + delay.Value;
            return publisher.PublishAsync(@event: @event, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <summary>Publish a batch of events.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="events">The events to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<IList<ScheduledResult>?> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                                         IList<EventContext<TEvent>> events,
                                                                         TimeSpan? delay,
                                                                         CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = delay is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow + delay.Value;
            return publisher.PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <summary>Publish an event.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="event">The event to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<ScheduledResult?> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                                  TEvent @event,
                                                                  TimeSpan? delay,
                                                                  CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = delay is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow + delay.Value;
            return publisher.PublishAsync(@event: @event, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <summary>Publish a batch of events.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="events">The events to publish.</param>
        /// <param name="delay">The duration of time to wait before the event is available on the bus for consumption.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<IList<ScheduledResult>?> PublishAsync<TEvent>(this IEventPublisher publisher,
                                                                         IList<TEvent> events,
                                                                         TimeSpan? delay,
                                                                         CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var scheduled = delay is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow + delay.Value;
            return publisher.PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        #endregion

        #region Cancellation

        /// <summary>Cancel a scheduled event.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="result">The scheduling result.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task CancelAsync<TEvent>(this IEventPublisher publisher,
                                               ScheduledResult result,
                                               CancellationToken cancellationToken = default)
            where TEvent : class
        {
            return publisher.CancelAsync<TEvent>(id: result.Id, cancellationToken: cancellationToken);
        }

        /// <summary>Cancel a batch of scheduled events.</summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to extend.</param>
        /// <param name="results">The scheduling results.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task CancelAsync<TEvent>(this IEventPublisher publisher,
                                               IList<ScheduledResult> results,
                                               CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var ids = results.Select(r => r.Id).ToList();
            return publisher.CancelAsync<TEvent>(ids: ids, cancellationToken: cancellationToken);
        }

        #endregion

    }
}
