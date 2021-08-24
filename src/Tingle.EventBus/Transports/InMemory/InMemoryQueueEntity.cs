using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryQueueEntity
    {
        private readonly SemaphoreSlim messageAvailable = new(0);
        private readonly SemaphoreSlim updateLock = new(1);
        private readonly Queue<InMemoryQueueMessage> queue = new();
        private readonly TimeSpan deliveryDelay;

        public InMemoryQueueEntity(string name, TimeSpan deliveryDelay)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.deliveryDelay = deliveryDelay;
        }

        public string Name { get; }

        public async Task EnqueueAsync(InMemoryQueueMessage item, CancellationToken cancellationToken = default)
        {
            await updateLock.WaitAsync(cancellationToken);
            try
            {
                queue.Enqueue(item);
                messageAvailable.Release(1);
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }

        public async Task EnqueueBatchAsync(IEnumerable<InMemoryQueueMessage> items, CancellationToken cancellationToken = default)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            foreach (var item in items) await EnqueueAsync(item, cancellationToken);
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
