using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Represents a registration for an event.
    /// </summary>
    public class EventRegistration : IEquatable<EventRegistration>
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
        public string EventName { get; set; }

        /// <summary>
        /// The name of the transport used for the event.
        /// </summary>
        public string TransportName { get; set; }

        /// <summary>
        /// The type used for serializing and deserializing events.
        /// This type must implement <see cref="Serialization.IEventSerializer"/>.
        /// </summary>
        public Type EventSerializerType { get; set; }

        /// <summary>
        /// The list of consumers registered for this event.
        /// </summary>
        /// <remarks>
        /// This is backed by a <see cref="HashSet{T}"/> to ensure no duplicates.
        /// </remarks>
        internal ICollection<EventConsumerRegistration> Consumers { get; set; } = new HashSet<EventConsumerRegistration>();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as EventRegistration);

        /// <inheritdoc/>
        public bool Equals(EventRegistration other)
        {
            return other != null && EqualityComparer<Type>.Default.Equals(EventType, other.EventType);
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
    }
}
