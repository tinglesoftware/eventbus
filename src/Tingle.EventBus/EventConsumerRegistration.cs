using System;

namespace Tingle.EventBus
{
    /// <summary>
    /// Represents a registration for a consumer of an event.
    /// </summary>
    public class EventConsumerRegistration : EventRegistration
    {
        /// <summary>
        /// Creates an instance of <see cref="EventConsumerRegistration"/>
        /// </summary>
        /// <param name="eventType">The type of event handled.</param>
        /// <param name="consumerType">The type of consumer handling the event.</param>
        public EventConsumerRegistration(Type eventType, Type consumerType) : base(eventType)
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
