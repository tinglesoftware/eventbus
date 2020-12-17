using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// An envelope of a event as represented when serialized.
    /// </summary>
    public class MessageEnvelope
    {
        /// <summary>
        /// The unique identifier of the event.
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// The unique identifier of the request associated with the event.
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// A value shared between related events.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// The unique identifier of the conversation.
        /// </summary>
        public string ConversationId { get; set; }

        /// <summary>
        /// The unique identifier of the initiator of the event.
        /// </summary>
        public string InitiatorId { get; set; }

        /// <summary>
        /// The event published or to be published.
        /// </summary>
        public object Event { get; set; }

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
        /// </summary>
        public IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Information about the host on which the event was generated.
        /// </summary>
        public HostInfo Host { get; set; }
    }
}
