namespace Tingle.EventBus.Serialization;

/// <summary>
/// An envelope of a event as represented when serialized.
/// </summary>
public class EventEnvelope : IEventEnvelope
{
    /// <inheritdoc/>
    public string? Id { get; init; }

    /// <inheritdoc/>
    public string? RequestId { get; init; }

    /// <inheritdoc/>
    public string? CorrelationId { get; init; }

    /// <inheritdoc/>
    public string? InitiatorId { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset? Expires { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset? Sent { get; init; }

    /// <inheritdoc/>
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public HostInfo? Host { get; init; }
}

/// <summary>
/// An envelope of a event as represented when serialized.
/// </summary>
public class EventEnvelope<T> : EventEnvelope, IEventEnvelope<T> where T : class
{
    /// <inheritdoc/>
    public T? Event { get; init; }
}
