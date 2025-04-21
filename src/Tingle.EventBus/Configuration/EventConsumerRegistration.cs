using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Represents a registration for a consumer of an event.
/// </summary>
public class EventConsumerRegistration : IEquatable<EventConsumerRegistration?>
{
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

    internal ConsumeDelegate Consume { get; init; } = default!;

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

    private EventConsumerRegistration([DynamicallyAccessedMembers(TrimmingHelper.Consumer)] Type consumerType, bool deadletter, ConsumeDelegate consume) // this private to enforce use of the factory methods which cater to generics in AOT
    {
        ConsumerType = consumerType ?? throw new ArgumentNullException(nameof(consumerType));
        Deadletter = deadletter;
        Consume = consume ?? throw new ArgumentNullException(nameof(consume));
    }

    /// <summary>Create a new instance of <see cref="EventConsumerRegistration"/> for the specified types.</summary>
    /// <typeparam name="TEvent">The type of event.</typeparam>
    /// <typeparam name="TConsumer">The type of consumer.</typeparam>
    public static EventConsumerRegistration Create<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>()
        where TEvent : class
        where TConsumer : IEventConsumer<TEvent>
        => new(typeof(TConsumer), false, ExecutionHelper.ConsumeAsync<TEvent>);

    /// <summary>Create a new instance of <see cref="EventConsumerRegistration"/> for the specified types.</summary>
    /// <typeparam name="TEvent">The type of event.</typeparam>
    /// <typeparam name="TConsumer">The type of consumer.</typeparam>
    public static EventConsumerRegistration CreateDeadLettered<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>()
        where TEvent : class
        where TConsumer : IDeadLetteredEventConsumer<TEvent>
        => new(typeof(TConsumer), true, ExecutionHelper.ConsumeAsync<TEvent>);

    /// <summary>Create a new instance of <see cref="EventConsumerRegistration"/> for the specified types.</summary>
    /// <param name="eventType">The type of event.</param>
    /// <param name="consumerType">The type of consumer.</param>
    /// <param name="deadletter">Indicates if the consumer should be connected to the dead-letter entity.</param>
    [RequiresDynamicCode(MessageStrings.GenericsDynamicCodeMessage)]
    [RequiresUnreferencedCode(MessageStrings.GenericsUnreferencedCodeMessage)]
    public static EventConsumerRegistration Create([DynamicallyAccessedMembers(TrimmingHelper.Event)] Type eventType, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] Type consumerType, bool deadletter)
        => new(consumerType, deadletter, ExecutionHelper.MakeDelegate(eventType, consumerType));

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

/// <summary>Delegate for consuming an event payload to an <see cref="EventContext"/>.</summary>
/// <param name="consumer">The <see cref="IEventConsumer"/> to use.</param>
/// <param name="registration">The <see cref="EventRegistration"/> for the current event.</param>
/// <param name="ecr">The <see cref="EventConsumerRegistration"/> for the current event.</param>
/// <param name="context">The context containing the event.</param>
/// <param name="cancellationToken"></param>
internal delegate Task ConsumeDelegate(IEventConsumer consumer, EventRegistration registration, EventConsumerRegistration ecr, EventContext context, CancellationToken cancellationToken = default);
