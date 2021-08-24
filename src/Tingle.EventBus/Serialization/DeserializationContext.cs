using System;
using System.Net.Mime;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// Context for performing deserialization.
    /// </summary>
    public class DeserializationContext
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="body">The <see cref="BinaryData"/> containing the raw data.</param>
        /// <param name="registration">Registration for this event being deserialized.</param>
        /// <param name="identifier">Identifier given the transport for the event to be deserialized.</param>
        public DeserializationContext(BinaryData body, EventRegistration registration, string? identifier = null)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Registration = registration ?? throw new ArgumentNullException(nameof(registration));
            Identifier = identifier;
        }

        /// <summary>
        /// The <see cref="BinaryData"/> containing the raw data.
        /// </summary>
        public BinaryData Body { get; }

        /// <summary>
        /// Registration for this event being deserialized.
        /// </summary>
        public EventRegistration Registration { get; }

        /// <summary>
        /// Identifier given the transport for the event to be deserialized.
        /// </summary>
        public string? Identifier { get; }

        /// <summary>
        /// Type of content contained in the <see cref="Body"/>.
        /// </summary>
        public ContentType? ContentType { get; set; }
    }
}
