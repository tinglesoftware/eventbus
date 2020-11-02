using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Abstractions.Serialization
{
    public class MessageEnvelope
    {
        public string EventId { get; set; }
        public string RequestId { get; set; }
        public string CorrelationId { get; set; }
        public string ConversationId { get; set; }
        public string InitiatorId { get; set; }
        public object Event { get; set; }
        public DateTimeOffset? Expires { get; set; }
        public DateTimeOffset? Sent { get; set; }
        public IDictionary<string, object> Headers { get; set; }
        public HostInfo Host { get; set; }
    }
}
