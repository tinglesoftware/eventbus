using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus;

/// <summary>
/// Contract describing a publisher of events.
/// </summary>
public interface IEventPublisher
{
    /// <summary>Create an instance of <see cref="EventContext{T}"/>.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to be nested.</param>
    /// <param name="correlationId">The identifier of the event from which to create a child event.</param>
    /// <returns></returns>
    EventContext<TEvent> CreateEventContext<TEvent>(TEvent @event, string? correlationId = null)
        where TEvent : class;

    /// <summary>Publish an event.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="scheduled">
    /// The time at which the event should be availed for consumption.
    /// Set null for immediate availability.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                DateTimeOffset? scheduled = null,
                                                CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>Publish a batch of events.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="scheduled">
    /// The time at which the event should be availed for consumption.
    /// Set null for immediate availability.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                       DateTimeOffset? scheduled = null,
                                                       CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>Cancel a scheduled event.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="id">The scheduling identifier of the scheduled event.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>
    /// Cancel a batch of scheduled events.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="ids">The scheduling identifiers of the scheduled events.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
        where TEvent : class;
}
