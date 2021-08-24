using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryQueueMessage
    {
        /// <summary>
        /// Creates a new message.
        /// </summary>
        public InMemoryQueueMessage() { }

        /// <summary>
        /// Creates a new message from the specified string, using UTF-8 encoding.
        /// </summary>
        /// <param name="body">The payload of the message as a string.</param>
        public InMemoryQueueMessage(string body) : this(BinaryData.FromString(body)) { }

        /// <summary>
        /// Creates a new message from the specified payload.
        /// </summary>
        /// <param name="body">The payload of the message in bytes.</param>
        public InMemoryQueueMessage(byte[] body) : this(BinaryData.FromBytes(body)) { }

        /// <summary>
        /// Creates a new message from specified <see cref="BinaryData"/> instance.
        /// </summary>
        /// <param name="body">The payload of the message.</param>
        public InMemoryQueueMessage(BinaryData body)
        {
            Body = body;
        }

        /// <summary>
        /// Gets or sets the content type descriptor.
        /// </summary>
        /// <value>RFC2045 Content-Type descriptor.</value>
        /// <remarks>
        /// Optionally describes the payload of the message, with a descriptor
        /// following the format of RFC2045, Section 5, for example "application/json".
        /// </remarks>
        public string? ContentType { get; set; }

        /// <summary>
        /// Gets or sets the a correlation identifier.
        /// </summary>
        /// <value>Correlation identifier.</value>
        /// <remarks>
        /// Allows an application to specify a context for the message for the
        /// purposes of correlation, for example reflecting the <see cref="MessageId"/>
        /// of a message that is being replied to.
        /// </remarks>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the MessageId to identify the message.
        /// </summary>
        /// <remarks>
        /// The message identifier is an application-defined value that uniquely
        /// identifies the message and its payload. The identifier is a free-form
        /// string and can reflect a GUID or an identifier derived from the
        /// application context.
        /// </remarks>
        public string? MessageId { get; set; }

        /// <summary>
        /// Gets or sets the body of the message.
        /// </summary>
        public BinaryData? Body { get; set; }

        /// <summary>
        /// Gets the application properties bag, which can be used for custom message metadata.
        /// </summary>
        /// <remarks>
        /// Only following value types are supported: byte, sbyte, char, short, ushort, int, uint,
        /// long, ulong, float, double, decimal, bool, Guid, string, Uri, DateTime, DateTimeOffset,
        /// TimeSpan
        /// </remarks>
        public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}
