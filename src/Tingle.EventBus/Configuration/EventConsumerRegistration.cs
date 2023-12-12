using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Represents a registration for a consumer of an event.
/// </summary>
public class EventConsumerRegistration : IEquatable<EventConsumerRegistration?>
{
    /// <summary>
    /// Creates an instance of <see cref="EventConsumerRegistration"/>.
    /// </summary>
    /// <param name="consumerType">The type of consumer handling the event.</param>
    /// <param name="deadletter">Whether the consumer should be connected to the dead-letter entity.</param>
    public EventConsumerRegistration(Type consumerType, bool deadletter)
    {
        ConsumerType = consumerType ?? throw new ArgumentNullException(nameof(consumerType));
        Deadletter = deadletter;
    }

    /// <summary>
    /// The type of consumer handling the event.
    /// </summary>
    [DynamicallyAccessedMembers(TrimmingHelper.Consumer)]
    public Type ConsumerType { get; }

    /// <summary>
    /// Gets or sets a value indicating if the consumer should be connected to the dead-letter entity.
    /// For transports that do not support dead-letter entities, a separate queue is created.
    /// When set to <see langword="true"/>, you must use <see cref="IDeadLetteredEventConsumer{T}"/>
    /// to consume events.
    /// </summary>
    public bool Deadletter { get; }

    /// <summary>
    /// The name generated for the consumer.
    /// </summary>
    public string? ConsumerName { get; set; }

    /// <summary>
    /// The behaviour for unhandled errors when consuming events via the
    /// <see cref="IEventConsumer{T}.ConsumeAsync(EventContext{T}, CancellationToken)"/>
    /// method.
    /// When set to <see langword="null"/>, the transport's default behaviour is used.
    /// Depending on the transport, the event may be delayed for re-consumption
    /// or added back to the entity or availed to another processor/consumer instance.
    /// Defaults to <see langword="null"/>.
    /// When this value is set, it overrides the default value set on the transport or the bus.
    /// <br/>
    /// When a resilience pipeline is in force, only errors not handled by it will be subject to the value set here.
    /// </summary>
    public UnhandledConsumerErrorBehaviour? UnhandledErrorBehaviour { get; set; }

    /// <summary>
    /// Gets a key/value collection that can be used to organize and share data across components
    /// of the event bus such as the bus, the transport or the serializer.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Sets <see cref="UnhandledErrorBehaviour"/> to <paramref name="behaviour"/>.
    /// </summary>
    /// <param name="behaviour">The value to set.</param>
    /// <returns>The <see cref="EventConsumerRegistration"/> for further configuration.</returns>
    public EventConsumerRegistration OnError(UnhandledConsumerErrorBehaviour? behaviour)
    {
        UnhandledErrorBehaviour = behaviour;
        return this;
    }

    /// <summary>
    /// Sets <see cref="UnhandledErrorBehaviour"/> to <see cref="UnhandledConsumerErrorBehaviour.Deadletter"/>.
    /// </summary>
    /// <returns>The <see cref="EventConsumerRegistration"/> for further configuration.</returns>
    public EventConsumerRegistration OnErrorDeadletter() => OnError(UnhandledConsumerErrorBehaviour.Deadletter);

    /// <summary>
    /// Sets <see cref="UnhandledErrorBehaviour"/> to <see cref="UnhandledConsumerErrorBehaviour.Discard"/>.
    /// </summary>
    /// <returns>The <see cref="EventConsumerRegistration"/> for further configuration.</returns>
    public EventConsumerRegistration OnErrorDiscard() => OnError(UnhandledConsumerErrorBehaviour.Discard);

    #region Equality Overrides

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as EventConsumerRegistration);

    /// <inheritdoc/>
    public bool Equals(EventConsumerRegistration? other)
    {
        return other is not null &&
               EqualityComparer<Type>.Default.Equals(ConsumerType, other.ConsumerType) &&
               Deadletter == other.Deadletter;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ConsumerType, Deadletter);

    ///
    public static bool operator ==(EventConsumerRegistration? left, EventConsumerRegistration? right)
    {
        return EqualityComparer<EventConsumerRegistration?>.Default.Equals(left, right);
    }

    ///
    public static bool operator !=(EventConsumerRegistration? left, EventConsumerRegistration? right) => !(left == right);

    #endregion
}
