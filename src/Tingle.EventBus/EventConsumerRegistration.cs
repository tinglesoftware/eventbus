using System;

namespace Tingle.EventBus
{
    /// <summary>
    /// Represents a registration for a consumer of an event.
    /// </summary>
    public class EventConsumerRegistration
    {
        /// <summary>
        /// Creates an instance of <see cref="EventConsumerRegistration"/>
        /// </summary>
        /// <param name="eventType">The type of event handled.</param>
        /// <param name="consumerType">The type of consumer handling the event.</param>
        public EventConsumerRegistration(Type eventType, Type consumerType)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            ConsumerType = consumerType ?? throw new ArgumentNullException(nameof(consumerType));
        }

        /// <summary>
        /// The type of event handled.
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// The type of consumer handling the event.
        /// </summary>
        public Type ConsumerType { get; }

        /// <summary>
        /// The name generated for the event.
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// The name generated for the consumer.
        /// </summary>
        public string ConsumerName { get; set; }

        /// <summary>
        /// The type used for serializing and deserializing events.
        /// This type must implement <see cref="Serialization.IEventSerializer"/>.
        /// </summary>
        public Type EventSerializerType { get; set; } = typeof(Serialization.IEventSerializer);
    }
}
