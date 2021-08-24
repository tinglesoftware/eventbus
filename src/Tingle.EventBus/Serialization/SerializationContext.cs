using System;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// Context for performing serialization.
    /// </summary>
    public class SerializationContext<T> where T : class
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="event">The event to be serialized.</param>
        /// <param name="registration">Registration for this event being deserialized.</param>
        public SerializationContext(EventContext<T> @event, EventRegistration registration)
        {
            Registration = registration ?? throw new ArgumentNullException(nameof(registration));
            Event = @event ?? throw new ArgumentNullException(nameof(@event));
        }

        /// <summary>
        /// The event to be serialized.
        /// </summary>
        public EventContext<T> Event { get; }

        /// <summary>
        /// The <see cref="BinaryData"/> containing the raw data.
        /// </summary>
        public BinaryData? Body { get; internal set; }

        /// <summary>
        /// Registration for this event being deserialized.
        /// </summary>
        public EventRegistration Registration { get; }
    }
}
