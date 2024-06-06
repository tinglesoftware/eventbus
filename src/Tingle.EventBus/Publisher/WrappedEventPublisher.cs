using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus;

/// <summary>
/// An implementation of <see cref="IEventPublisher"/>
/// that wraps another <see cref="IEventPublisher"/>.
/// </summary>
/// <param name="inner">The instance of <see cref="IEventPublisher"/> to use for operations.</param>
public class WrappedEventPublisher(IEventPublisher inner) : IEventPublisher
{
    private readonly IEventPublisher inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc/>
    public Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(string id, CancellationToken cancellationToken = default) where TEvent : class
    {
        return inner.CancelAsync<TEvent>(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<string> ids, CancellationToken cancellationToken = default) where TEvent : class
    {
        return inner.CancelAsync<TEvent>(ids, cancellationToken);
    }

    /// <inheritdoc/>
    public EventContext<TEvent> CreateEventContext<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(TEvent @event, string? correlationId = null)
        where TEvent : class
    {
        return inner.CreateEventContext<TEvent>(@event, correlationId);
    }

    /// <inheritdoc/>
    public Task<ScheduledResult?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                          DateTimeOffset? scheduled = null,
                                                                                                          CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return inner.PublishAsync<TEvent>(@event, scheduled, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IList<ScheduledResult>?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                                 DateTimeOffset? scheduled = null,
                                                                                                                 CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return inner.PublishAsync<TEvent>(@events, scheduled, cancellationToken);
    }
}
