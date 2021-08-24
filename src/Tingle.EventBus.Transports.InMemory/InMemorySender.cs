using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.InMemory
{
    internal sealed class InMemorySender // TODO: handle disposing
    {
        private static readonly TimeSpan waitTimeout = TimeSpan.FromSeconds(1);

        private readonly ChannelWriter<InMemoryMessage> writer;
        private readonly SemaphoreSlim available = new(0);
        private readonly SemaphoreSlim updateLock = new(1);
        private readonly CancellationTokenSource stoppingCts = new();

        private List<InMemoryMessage> items = new();

        public InMemorySender(ChannelWriter<InMemoryMessage> writer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _ = RunAsync(stoppingCts.Token);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // wait to be notified of an item added
                await available.WaitAsync(waitTimeout, cancellationToken);

                var cached = items.ToList(); // just incase it is changed
                foreach (var i in cached)
                {
                    if (i.Scheduled <= DateTimeOffset.UtcNow)
                    {
                        await writer.WriteAsync(i, cancellationToken);
                    }
                }
            }
        }

        /// <summary>
        /// Closes the producer.
        /// </summary>
        /// <param name="cancellationToken">And optional <see cref="CancellationToken"/> to signal the request to cancel the operation.</param>
        /// <returns></returns>
        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            stoppingCts.Cancel();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Send a message to the associated entity in memory.
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">And optional <see cref="CancellationToken"/> to signal the request to cancel the operation.</param>
        /// <returns></returns>
        public async Task SendMessageAsync(InMemoryMessage message, CancellationToken cancellationToken = default)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            await updateLock.WaitAsync(cancellationToken);
            try
            {
                // ensure we do not have any duplicates for the sequence number.
                if (items.Any(m => m.SequenceNumber == message.SequenceNumber))
                {
                    throw new ArgumentException($"An item with the sequence number {message.SequenceNumber} is already present.", nameof(message));
                }

                items.Add(message);
                available.Release();
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }

        /// <summary>
        /// Sends a set of messages to the associated entity in memory.
        /// </summary>
        /// <param name="messages">The set of messages to send.</param>
        /// <param name="cancellationToken">And optional <see cref="CancellationToken"/> to signal the request to cancel the operation.</param>
        /// <returns></returns>
        public async Task SendMessagesAsync(IEnumerable<InMemoryMessage> messages, CancellationToken cancellationToken = default)
        {
            if (messages is null) throw new ArgumentNullException(nameof(messages));

            await updateLock.WaitAsync(cancellationToken);
            try
            {
                // ensure we do not have any duplicates for the sequence number.
                var duplicates = messages.Select(m => m.SequenceNumber).Intersect(items.Select(i => i.SequenceNumber)).ToList();
                foreach (var message in messages)
                {
                    if (messages.Any(m => m.SequenceNumber == message.SequenceNumber))
                    {
                        throw new ArgumentException($"An item with the sequence number {message.SequenceNumber} is already present.", nameof(message));
                    }
                }

                items.AddRange(messages);
                available.Release();
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }

        /// <summary>
        /// Cancels a message that was scheduled.
        /// </summary>
        /// <param name="sequenceNumber">
        /// The <see cref="InMemoryReceivedMessage.SequenceNumber"/> of the message to be cancelled.
        /// </param>
        /// <param name="cancellationToken">And optional <see cref="CancellationToken"/> to signal the request to cancel the operation.</param>
        /// <returns></returns>
        public async Task CancelScheduledMessageAsync(long sequenceNumber, CancellationToken cancellationToken)
        {
            await updateLock.WaitAsync(cancellationToken);
            try
            {
                // get matching
                var matching = items.SingleOrDefault(m => m.SequenceNumber == sequenceNumber);
                if (matching is null)
                {
                    throw new ArgumentException($"An item with the sequence number {sequenceNumber} does not exist.", nameof(sequenceNumber));
                }

                // make new items and recreate the queue
                items = items.AsEnumerable().Except(new[] { matching }).ToList();
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }

        /// <summary>
        /// Cancels a set of messages that were scheduled.
        /// </summary>
        /// <param name="sequenceNumbers">
        /// The set of <see cref="InMemoryReceivedMessage.SequenceNumber"/> of the messages to be cancelled.
        /// </param>
        /// <param name="cancellationToken">And optional <see cref="CancellationToken"/> to signal the request to cancel the operation.</param>
        /// <returns></returns>
        public async Task CancelScheduledMessagesAsync(IEnumerable<long> sequenceNumbers, CancellationToken cancellationToken)
        {
            await updateLock.WaitAsync(cancellationToken);
            try
            {
                // get matching
                var matching = new List<InMemoryMessage>();
                foreach (var sn in sequenceNumbers)
                {
                    var item = items.SingleOrDefault(m => m.SequenceNumber == sn);
                    if (item is null)
                    {
                        throw new ArgumentException($"An item with the sequence number {sn} does not exist.", nameof(sn));
                    }
                }

                // make new items and recreate the queue
                items = items.AsEnumerable().Except(matching).ToList();
            }
            catch (Exception)
            {
                updateLock.Release();
            }
        }
    }
}
