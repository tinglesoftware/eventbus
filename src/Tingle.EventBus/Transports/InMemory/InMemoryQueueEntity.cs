using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryQueueEntity
    {
        private readonly SemaphoreSlim messageAvailable = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<InMemoryQueueMessage> queue = new ConcurrentQueue<InMemoryQueueMessage>();
        private readonly TimeSpan deliveryDelay;

        public InMemoryQueueEntity(string name, TimeSpan deliveryDelay)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.deliveryDelay = deliveryDelay;
        }

        public string Name { get; }

        public void Enqueue(InMemoryQueueMessage item)
        {
            queue.Enqueue(item);
            messageAvailable.Release(1);
        }

        public void EnqueueBatch(IEnumerable<InMemoryQueueMessage> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            foreach (var item in items) Enqueue(item);
        }

        public async Task<InMemoryQueueMessage> DequeueAsync(CancellationToken cancellationToken = default)
        {
            // wait to be notified of an item in the queue
            await messageAvailable.WaitAsync(cancellationToken);

            if (queue.TryDequeue(out var result))
            {
                // if we have a delivery delay, apply id
                if (deliveryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(deliveryDelay, cancellationToken);
                }

                return result;
            }

            throw new NotImplementedException("This should not happen!");
        }
    }
}
