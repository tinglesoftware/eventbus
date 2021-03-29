using Polly.Retry;
using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Registrations
{
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
        public string ConsumerName { get; set; }

        /// <summary>
        /// The retry policy to apply when consuming events.
        /// This is an outter wrapper around the <see cref="IEventConsumer{T}.ConsumeAsync(EventContext{T}, System.Threading.CancellationToken)"/>
        /// method.
        /// When set to <see langword="null"/>, the method is only invoked once.
        /// Defaults to <see langword="null"/>.
        /// When this value is set, it overrides the default value set on the transport or the bus.
        /// </summary>
        /// <remarks>
        /// When a value is provided, the transport may extend the lock for the
        /// message until the execution with with retry policy completes successfully or not.
        /// In such a case, ensure the execution timeout (sometimes called the visibility timeout
        /// or lock duration) is set to accomodate the longest possible duration of the retry policy.
        /// </remarks>
        public AsyncRetryPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Gets a key/value collection that can be used to organize and share data across components
        /// of the event bus such as the bus, the transport or the serializer.
        /// </summary>
        public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as EventConsumerRegistration);

        /// <inheritdoc/>
        public bool Equals(EventConsumerRegistration other)
        {
            return other != null && EqualityComparer<Type>.Default.Equals(ConsumerType, other.ConsumerType);
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
    }
}
