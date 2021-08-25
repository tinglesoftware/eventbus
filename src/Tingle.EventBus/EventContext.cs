using System;
using System.Collections.Generic;
using System.Net.Mime;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus
{
    /// <summary>
    /// Generic context for an event.
    /// </summary>
    public abstract class EventContext : WrappedEventPublisher
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to use.</param>
        protected EventContext(IEventPublisher publisher) : base(publisher) { }

        /// <summary>
        /// The unique identifier of the event.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The unique identifier of the request associated with the event.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// A value shared between related events.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// The unique identifier of the initiator of the event.
        /// </summary>
        public string? InitiatorId { get; set; }

        /// <summary>
        /// The specific time at which the event expires.
        /// </summary>
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// The specific time the event was sent.
        /// </summary>
        public DateTimeOffset? Sent { get; set; }

        /// <summary>
        /// The headers published alongside the event.
        /// The keys are case insensitive.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The content type used to serialize and deserialize the event to/from a stream of bytes.
        /// Setting a value, instructs the serializer how to write the event content on the transport.
        /// The serializer used for the event must support the value set.
        /// When set to <see langword="null"/>, the serializer used for the event decides what
        /// content type to use depending on its implementation.
        /// For the default implementation, see <see cref="DefaultJsonEventSerializer"/>.
        /// </summary>
        public ContentType? ContentType { get; set; }

        /// <summary>
        /// Gets or sets a key/value collection that can be used to share data within the scope of this context.
        /// </summary>
        /// <remarks>
        /// This information should not be passed on to serialization.
        /// </remarks>
        public IDictionary<string, object?> Items { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Identifier given by the transport for the event.
        /// </summary>
        public string? TransportIdentifier { get; internal init; }
    }

    /// <summary>
    /// The context for a specific event.
    /// </summary>
    /// <typeparam name="T">The type of event carried.</typeparam>
    public class EventContext<T> : EventContext where T : class
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to use.</param>
        /// <param name="event">Value for <see cref="Event"/>.</param>
        public EventContext(IEventPublisher publisher, T @event) : base(publisher)
        {
            Event = @event;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="publisher">The <see cref="IEventPublisher"/> to use.</param>
        /// <param name="event">Value for <see cref="Event"/>.</param>
        /// <param name="envelope">Envelope containing details for the event.</param>
        /// <param name="contentType">Value for <see cref="EventContext.ContentType"/></param>
        public EventContext(IEventPublisher publisher, T @event, IEventEnvelope envelope, ContentType? contentType) : this(publisher, @event)
        {
            Id = envelope.Id;
            RequestId = envelope.RequestId;
            CorrelationId = envelope.CorrelationId;
            InitiatorId = envelope.InitiatorId;
            Expires = envelope.Expires;
            Sent = envelope.Sent;
            Headers = envelope.Headers;
            ContentType = contentType;
        }

        // marked internal because of the forced null forgiving operator
        internal EventContext(IEventPublisher publisher, IEventEnvelope<T> envelope, ContentType? contentType, string? transportIdentifier)
            : this(publisher, envelope.Event!, envelope, contentType)
        {
            TransportIdentifier = transportIdentifier;
        }

        /// <summary>
        /// The event published or to be published.
        /// </summary>
        public T Event { get; set; }
    }
}
