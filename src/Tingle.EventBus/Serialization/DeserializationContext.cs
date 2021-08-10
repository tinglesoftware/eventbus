using System;
using System.IO;
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
        /// <param name="stream">
        /// The <see cref="System.IO.Stream"/> containing the raw data.
        /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
        /// </param>
        /// <param name="registration">Registration for this event being deserialized.</param>
        /// <param name="identifier">Identifier given the transport for the event to be deserialized.</param>
        public DeserializationContext(Stream stream,
                                      EventRegistration registration,
                                      string? identifier = null)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
            {
                throw new InvalidOperationException("The supplied stream must be readable");
            }

            Registration = registration;
            Identifier = identifier;
        }

        /// <summary>
        /// The <see cref="System.IO.Stream"/> containing the raw data.
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// Registration for this event being deserialized.
        /// </summary>
        public EventRegistration Registration { get; }

        /// <summary>
        /// Identifier given the transport for the event to be deserialized.
        /// </summary>
        public string? Identifier { get; }

        /// <summary>
        /// Type of content contained in the <see cref="Stream"/>.
        /// </summary>
        public ContentType? ContentType { get; set; }
    }
}
