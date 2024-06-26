﻿using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports;

/// <summary>
/// Contract for implementing an event bus transport.
/// </summary>
public interface IEventBusTransport
{
    /// <summary>The name of the transport.</summary>
    string Name { get; }

    /// 
    void Initialize(EventBusTransportRegistration registration);

    /// <summary>Triggered when the bus host is ready to start.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Triggered when the bus host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Publish an event on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="registration">The registration for the event.</param>
    /// <param name="scheduled">
    /// The time at which the event should be availed for consumption.
    /// Set <see langword="null"/> for immediate availability.
    /// </param>
    /// <param name="cancellationToken"></param>
    Task<ScheduledResult?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                   EventRegistration registration,
                                                                                                   DateTimeOffset? scheduled = null,
                                                                                                   CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>Publish a batch of events on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="registration">The registration for the events.</param>
    /// <param name="scheduled">
    /// The time at which the event should be availed for consumption.
    /// Set <see langword="null"/> for immediate availability.
    /// </param>
    /// <param name="cancellationToken"></param>
    Task<IList<ScheduledResult>?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                          EventRegistration registration,
                                                                                                          DateTimeOffset? scheduled = null,
                                                                                                          CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>Cancel a scheduled event on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="id">The scheduling identifier of the scheduled event.</param>
    /// <param name="registration">The registration for the event.</param>
    /// <param name="cancellationToken"></param>
    Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(string id,
                                                                                EventRegistration registration,
                                                                                CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>Cancel a batch of scheduled events on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="ids">The scheduling identifiers of the scheduled events.</param>
    /// <param name="registration">The registration for the events.</param>
    /// <param name="cancellationToken"></param>
    Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<string> ids,
                                                                                EventRegistration registration,
                                                                                CancellationToken cancellationToken = default)
        where TEvent : class;

    ///
    EventBusTransportOptions? GetOptions();
}
