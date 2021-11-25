using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Represents a registration for an event.
/// </summary>
public class EventRegistration : IEquatable<EventRegistration?>
{
    /// <summary>
    /// Creates an instance of <see cref="EventRegistration"/>.
    /// </summary>
    /// <param name="eventType">The type of event handled.</param>
    public EventRegistration(Type eventType)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
    }

    /// <summary>
    /// The type of event handled.
    /// </summary>
    public Type EventType { get; }

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
    /// THe preferred format to use when generating identifiers for the event.
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
    public Type? EventSerializerType { get; set; }

    /// <summary>
    /// The list of consumers registered for this event.
    /// </summary>
    /// <remarks>
    /// This is backed by a <see cref="HashSet{T}"/> to ensure no duplicates.
    /// </remarks>
    public ICollection<EventConsumerRegistration> Consumers { get; set; } = new HashSet<EventConsumerRegistration>();

    /// <summary>
    /// Gets a key/value collection that can be used to organize and share data across components
    /// of the event bus such as the bus, the transport or the serializer.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    #region Equality Overrides

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as EventRegistration);

    /// <inheritdoc/>
    public bool Equals(EventRegistration? other)
    {
        return other is not null && EqualityComparer<Type>.Default.Equals(EventType, other.EventType);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => EventType.GetHashCode();

    ///
    public static bool operator ==(EventRegistration left, EventRegistration right)
    {
        return EqualityComparer<EventRegistration>.Default.Equals(left, right);
    }

    ///
    public static bool operator !=(EventRegistration left, EventRegistration right) => !(left == right);

    #endregion
}
