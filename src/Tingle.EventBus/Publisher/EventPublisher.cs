using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus;

internal class EventPublisher(EventBus bus) : IEventPublisher
{
    /// <inheritdoc/>
    public EventContext<TEvent> CreateEventContext<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(TEvent @event, string? correlationId = null)
        where TEvent : class
    {
        return new EventContext<TEvent>(this, @event);
    }

    /// <inheritdoc/>
    public async Task<ScheduledResult?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                                DateTimeOffset? scheduled = null,
                                                                                                                CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return await bus.PublishAsync(@event, scheduled, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IList<ScheduledResult>?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                                       DateTimeOffset? scheduled = null,
                                                                                                                       CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return await bus.PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(string id, CancellationToken cancellationToken = default) where TEvent : class
    {
        await bus.CancelAsync<TEvent>(id: id, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<string> ids, CancellationToken cancellationToken = default) where TEvent : class
    {
        await bus.CancelAsync<TEvent>(ids: ids, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
