using System;
using System.Collections.Concurrent;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryQueueEntity
    {
        public InMemoryQueueEntity(string name, ConcurrentQueue<InMemoryQueueMessage> queue = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Queue = queue ?? new ConcurrentQueue<InMemoryQueueMessage>();
        }

        public string Name { get; }
        public ConcurrentQueue<InMemoryQueueMessage> Queue { get; }
    }
}
