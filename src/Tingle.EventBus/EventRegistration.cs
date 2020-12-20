using System;

namespace Tingle.EventBus
{
    /// <summary>
    /// Represents a registration for an event.
    /// </summary>
    public class EventRegistration
    {
        /// <summary>
        /// Creates an instance of <see cref="EventRegistration"/>
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
        public string EventName { get; set; }

        /// <summary>
        /// The type used for serializing and deserializing events.
        /// This type must implement <see cref="Serialization.IEventSerializer"/>.
        /// </summary>
        public Type EventSerializerType { get; set; }
    }
}
