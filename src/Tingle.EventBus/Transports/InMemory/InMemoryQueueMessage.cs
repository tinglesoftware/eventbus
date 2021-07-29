using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryQueueMessage
    {
        public InMemoryQueueMessage() { }
        public InMemoryQueueMessage(ReadOnlyMemory<byte> body) : this() => Body = body;
        public InMemoryQueueMessage(byte[] body) : this(new ReadOnlyMemory<byte>(body)) { }

        public string? ContentType { get; set; }
        public string? CorrelationId { get; set; }
        public string? MessageId { get; set; }
        public ReadOnlyMemory<byte> Body { get; set; }
        public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();
    }
}
