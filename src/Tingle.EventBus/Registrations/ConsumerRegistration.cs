using System;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Represents a registration for a consumer of an event.
    /// </summary>
    public class ConsumerRegistration : EventRegistration
    {
        /// <summary>
        /// Creates an instance of <see cref="ConsumerRegistration"/>.
        /// </summary>
        /// <param name="eventType">The type of event handled.</param>
        /// <param name="consumerType">The type of consumer handling the event.</param>
        public ConsumerRegistration(Type eventType, Type consumerType) : base(eventType)
        {
            ConsumerType = consumerType ?? throw new ArgumentNullException(nameof(consumerType));
        }

        /// <summary>
        /// Creates and instance of <see cref="ConsumerRegistration"/> from an instance
        /// of <see cref="EventRegistration"/>.
        /// </summary>
        /// <param name="eventRegistration">The event registration to copy from.</param>
        /// <param name="consumerType">The type of consumer handling the event.</param>
        public ConsumerRegistration(EventRegistration eventRegistration, Type consumerType) : base(eventRegistration)
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
    }
}
