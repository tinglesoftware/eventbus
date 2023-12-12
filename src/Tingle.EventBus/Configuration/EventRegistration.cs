﻿using Polly;
using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Represents a registration for an event.
/// </summary>
/// <param name="eventType">The type of event handled.</param>
public class EventRegistration(Type eventType) : IEquatable<EventRegistration?>
{
    /// <summary>
    /// The type of event handled.
    /// </summary>
    [DynamicallyAccessedMembers(TrimmingHelper.Event)]
    public Type EventType { get; } = eventType ?? throw new ArgumentNullException(nameof(eventType));

    /// <summary>
    /// The name generated for the event.
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// The preferred type of entity to use for the event.
    /// When set to <see langword="null"/>, the transport chooses a suitable one.
    /// Defaults to <see langword="null"/>.
    /// </summary>
    public EntityKind? EntityKind { get; set; }

    /// <summary>
    /// The preferred format to use when generating identifiers for the event.
    /// </summary>
    public EventIdFormat? IdFormat { get; set; }

    /// <summary>
    /// The name of the transport used for the event.
    /// </summary>
    public string? TransportName { get; set; }

    /// <summary>
    /// The type used for serializing and deserializing events.
    /// This type must implement <see cref="IEventSerializer"/>.
    /// </summary>
    [DynamicallyAccessedMembers(TrimmingHelper.Serializer)]
    public Type? EventSerializerType { get; set; }

    /// <summary>
    /// The duration of duplicate detection history that is maintained by the transport for the event.
    /// When not null, duplicate messages having the same <see cref="EventContext.Id"/>
    /// within the given duration will be discarded.
    /// Defaults to <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Duplicate detection can only be done on the transport layer because it requires persistent storage.
    /// This feature only works if the transport for the event supports duplicate detection.
    /// </remarks>
    public TimeSpan? DuplicateDetectionDuration { get; set; }

    /// <summary>
    /// The resiliency pipeline to apply specifically for this event.
    /// This is in addition to what may be provided by the SDKs for each transport.
    /// When provided alongside pipelines on the transport and the bus, it is used as the inner most pipeline.
    /// </summary>
    /// <remarks>
    /// When a value is provided, the transport may extend the lock for the
    /// message during consumption until the execution with resiliency pipeline completes successfully or not.
    /// In such a case, ensure the execution timeout (sometimes called the visibility timeout
    /// or lock duration) is set to accommodate the longest possible duration of the resiliency pipeline.
    /// </remarks>
    public ResiliencePipeline? ResiliencePipeline { get; set; }

    /// <summary>
    /// The list of consumers registered for this event.
    /// </summary>
    /// <remarks>
    /// This is backed by a <see cref="HashSet{T}"/> to ensure no duplicates.
    /// </remarks>
    public HashSet<EventConsumerRegistration> Consumers { get; } = [];

    /// <summary>
    /// Gets a key/value collection that can be used to organize and share data across components
    /// of the event bus such as the bus, the transport or the serializer.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>Whether the execution pipelines have been merged.</summary>
    internal bool MergedExecutionPipelines { get; set; } = false;

    /// <summary>The final resilience pipeline used in executions for the event and it's consumers.</summary>
    internal ResiliencePipeline ExecutionPipeline { get; set; } = ResiliencePipeline.Empty;

    #region Equality Overrides

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as EventRegistration);

    /// <inheritdoc/>
    public bool Equals(EventRegistration? other)
    {
        return other is not null &&
               EqualityComparer<Type>.Default.Equals(EventType, other.EventType);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(EventType);

    ///
    public static bool operator ==(EventRegistration? left, EventRegistration? right)
    {
        return EqualityComparer<EventRegistration?>.Default.Equals(left, right);
    }

    ///
    public static bool operator !=(EventRegistration? left, EventRegistration? right) => !(left == right);

    #endregion
}
