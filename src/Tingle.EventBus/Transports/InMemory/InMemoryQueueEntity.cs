﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryQueueEntity
    {
        private readonly SemaphoreSlim messageAvailable = new(0);
        private readonly SemaphoreSlim updateLock = new(1);
        private readonly TimeSpan deliveryDelay;
        private Queue<InMemoryQueueMessage> queue = new();

        public InMemoryQueueEntity(string name, TimeSpan deliveryDelay)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.deliveryDelay = deliveryDelay;
        }

        public string Name { get; }

        public async Task EnqueueAsync(InMemoryQueueMessage item, CancellationToken cancellationToken = default)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            await updateLock.WaitAsync(cancellationToken);
            try
            {
                // ensure we do not have any duplicates for the sequence number.
                if (queue.Any(m => m.SequenceNumber == item.SequenceNumber))
                {
                    throw new ArgumentException($"An item with the sequence number {item.SequenceNumber} is already present.", nameof(item));
                }

                queue.Enqueue(item);
                messageAvailable.Release(1);
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }

        public async Task EnqueueAsync(IEnumerable<InMemoryQueueMessage> items, CancellationToken cancellationToken = default)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            await updateLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var item in items)
                {
                    // ensure we do not have any duplicates for the sequence number.
                    if (queue.Any(m => m.SequenceNumber == item.SequenceNumber))
                    {
                        throw new ArgumentException($"An item with the sequence number {item.SequenceNumber} is already present.", nameof(item));
                    }

                    queue.Enqueue(item);
                    messageAvailable.Release(1);
                }
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }

        public async Task<InMemoryQueueMessage> DequeueAsync(CancellationToken cancellationToken = default)
        {
            // wait to be notified of an item in the queue
            await messageAvailable.WaitAsync(cancellationToken);

            // TODO: work on locking the updateLock since the queue is being modified

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

        public async Task RemoveAsync(long sequenceNumber, CancellationToken cancellationToken)
        {
            await updateLock.WaitAsync(cancellationToken);
            try
            {
                // get matching
                var matching = queue.SingleOrDefault(m => m.SequenceNumber == sequenceNumber);
                if (matching is null)
                {
                    throw new ArgumentException($"An item with the sequence number {sequenceNumber} does not exist.", nameof(sequenceNumber));
                }

                // make new items and recreate the queue
                var items = queue.AsEnumerable().Except(new[] { matching });
                queue = new Queue<InMemoryQueueMessage>(items);
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }

        public async Task RemoveAsync(IEnumerable<long> sequenceNumbers, CancellationToken cancellationToken)
        {
            await updateLock.WaitAsync(cancellationToken);
            try
            {
                // get matching
                var matching = new List<InMemoryQueueMessage>();
                foreach (var sn in sequenceNumbers)
                {
                    var item = queue.SingleOrDefault(m => m.SequenceNumber == sn);
                    if (item is null)
                    {
                        throw new ArgumentException($"An item with the sequence number {sn} does not exist.", nameof(sn));
                    }
                }

                // make new items and recreate the queue
                var items = queue.AsEnumerable().Except(matching);
                queue = new Queue<InMemoryQueueMessage>(items);
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }
    }
}
