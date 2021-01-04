using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryQueueEntity
    {
        private readonly ConcurrentQueue<InMemoryQueueMessage> queue = new ConcurrentQueue<InMemoryQueueMessage>();

        public InMemoryQueueEntity(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }

        public void Enqueue(InMemoryQueueMessage item) => queue.Enqueue(item);

        public bool TryDequeue(out InMemoryQueueMessage result) => queue.TryDequeue(out result);

        public void EnqueueBatch(IEnumerable<InMemoryQueueMessage> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                queue.Enqueue(item);
            }
        }
    }
}
