using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// An envelope of a event as represented when serialized.
/// </summary>
public class EventEnvelope : IEventEnvelope
{
    /// <inheritdoc/>
    public string? Id { get; set; }

    /// <inheritdoc/>
    public string? RequestId { get; set; }

    /// <inheritdoc/>
    public string? CorrelationId { get; set; }

    /// <inheritdoc/>
    public string? InitiatorId { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? Expires { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? Sent { get; set; }

    /// <inheritdoc/>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public HostInfo? Host { get; set; }
}

/// <summary>
/// An envelope of a event as represented when serialized.
/// </summary>
public class EventEnvelope<T> : EventEnvelope, IEventEnvelope<T> where T : class
{
    /// <inheritdoc/>
    public T? Event { get; set; }
}
