using Polly.Retry;
using Tingle.EventBus.Readiness;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Represents a registration for a consumer of an event.
/// </summary>
public class EventConsumerRegistration : IEquatable<EventConsumerRegistration>
{
    /// <summary>
    /// Creates an instance of <see cref="EventConsumerRegistration"/>.
    /// </summary>
    /// <param name="consumerType">The type of consumer handling the event.</param>
    public EventConsumerRegistration(Type consumerType)
    {
        ConsumerType = consumerType ?? throw new ArgumentNullException(nameof(consumerType));
    }

    /// <summary>
    /// The type of consumer handling the event.
    /// </summary>
    public Type ConsumerType { get; }

    /// <summary>
    /// The name generated for the consumer.
    /// </summary>
    public string? ConsumerName { get; set; }

    /// <summary>
    /// The type used for checking if the consumer is ready to consume events.
    /// This type must implement <see cref="IReadinessProvider"/>.
    /// </summary>
    public Type? ReadinessProviderType { get; set; }

    /// <summary>
    /// The tags to use when checking for readiness before the consumer can be asked to consume an event.
    /// This tags should match the health registrations for them to work with the default implementation
    /// of <see cref="IReadinessProvider"/>.
    /// </summary>
    public ICollection<string>? ReadinessTags { get; set; }

    /// <summary>
    /// The retry policy to apply when consuming events.
    /// This is an outter wrapper around the
    /// <see cref="IEventConsumer{T}.ConsumeAsync(EventContext{T}, System.Threading.CancellationToken)"/>
    /// method.
    /// When set to <see langword="null"/>, the method is only invoked once.
    /// Defaults to <see langword="null"/>.
    /// When this value is set, it overrides the default value set on the transport or the bus.
    /// </summary>
    /// <remarks>
    /// When a value is provided, the transport may extend the lock for the
    /// message until the execution with with retry policy completes successfully or not.
    /// In such a case, ensure the execution timeout (sometimes called the visibility timeout
    /// or lock duration) is set to accommodate the longest possible duration of the retry policy.
    /// </remarks>
    public AsyncRetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// The behaviour for unhandled errors when consuming events via the
    /// <see cref="IEventConsumer{T}.ConsumeAsync(EventContext{T}, System.Threading.CancellationToken)"/>
    /// method.
    /// When set to <see langword="null"/>, the transport's default behaviour is used.
    /// Depending on the transport, the event may be delayed for reconsumption
    /// or added back to the entity or availed to another processor/consumer instnance.
    /// Defaults to <see langword="null"/>.
    /// When this value is set, it overrides the default value set on the transport or the bus.
    /// <br/>
    /// When a <see cref="RetryPolicy"/> is in force, only errors not handled by it will be subject to the value set here.
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
    /// <returns>The <see cref="EventConsumerRegistration"/> for futher configuration.</returns>
    public EventConsumerRegistration OnError(UnhandledConsumerErrorBehaviour? behaviour)
    {
        UnhandledErrorBehaviour = behaviour;
        return this;
    }

    /// <summary>
    /// Sets <see cref="UnhandledErrorBehaviour"/> to <see cref="UnhandledConsumerErrorBehaviour.Deadletter"/>.
    /// </summary>
    /// <returns>The <see cref="EventConsumerRegistration"/> for futher configuration.</returns>
    public EventConsumerRegistration OnErrorDeadletter() => OnError(UnhandledConsumerErrorBehaviour.Deadletter);

    /// <summary>
    /// Sets <see cref="UnhandledErrorBehaviour"/> to <see cref="UnhandledConsumerErrorBehaviour.Discard"/>.
    /// </summary>
    /// <returns>The <see cref="EventConsumerRegistration"/> for futher configuration.</returns>
    public EventConsumerRegistration OnErrorDiscard() => OnError(UnhandledConsumerErrorBehaviour.Discard);

    #region Equality Overrides

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as EventConsumerRegistration);

    /// <inheritdoc/>
    public bool Equals(EventConsumerRegistration? other)
    {
        return other is not null && EqualityComparer<Type>.Default.Equals(ConsumerType, other.ConsumerType);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => ConsumerType.GetHashCode();

    ///
    public static bool operator ==(EventConsumerRegistration left, EventConsumerRegistration right)
    {
        return EqualityComparer<EventConsumerRegistration>.Default.Equals(left, right);
    }

    ///
    public static bool operator !=(EventConsumerRegistration left, EventConsumerRegistration right) => !(left == right);

    #endregion

}
