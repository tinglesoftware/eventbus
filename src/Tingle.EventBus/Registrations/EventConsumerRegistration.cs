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
