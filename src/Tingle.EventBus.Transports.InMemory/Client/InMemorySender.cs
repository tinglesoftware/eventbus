using System.Threading.Channels;

namespace Tingle.EventBus.Transports.InMemory.Client;

internal interface IInMemorySender : IDisposable
{
    Task CloseAsync(CancellationToken cancellationToken = default);
}

internal sealed class InMemorySender : IInMemorySender
{
    private static readonly TimeSpan waitTimeout = TimeSpan.FromSeconds(1);

    private readonly ChannelWriter<InMemoryMessage> writer;
    private readonly SequenceNumberGenerator sng;
    private readonly SemaphoreSlim available = new(0);
    private readonly SemaphoreSlim updateLock = new(1);
    private readonly CancellationTokenSource stoppingCts = new();

    private List<InMemoryMessage> items = [];

    public InMemorySender(string entityPath, ChannelWriter<InMemoryMessage> writer, SequenceNumberGenerator sng)
    {
        if (string.IsNullOrWhiteSpace(EntityPath = entityPath))
        {
            throw new ArgumentException($"'{nameof(entityPath)}' cannot be null or whitespace.", nameof(entityPath));
        }

        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.sng = sng ?? throw new ArgumentNullException(nameof(sng));
        _ = RunAsync(stoppingCts.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // wait to be notified of an item added
            await available.WaitAsync(waitTimeout, cancellationToken).ConfigureAwait(false);

            var cached = items.ToList(); // just in case it is changed
            foreach (var msg in cached)
            {
                if (msg.Scheduled <= DateTimeOffset.UtcNow)
                {
                    // write the message and remove it
                    await writer.WriteAsync(msg, cancellationToken).ConfigureAwait(false);
                    items.Remove(msg);
                }
            }
        }
    }

    public string EntityPath { get; }

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

        await updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            message.SequenceNumber = sng.Generate();
            items.Add(message);
            available.Release();
        }
        finally
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

        await updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // set sequence numbers
            foreach (var message in messages)
            {
                message.SequenceNumber = sng.Generate();
            }

            items.AddRange(messages);
            available.Release();
        }
        finally
        {
            updateLock.Release();
        }
    }

    /// <summary>
    /// Schedules a message to appear on the transport at a later time.
    /// </summary>
    /// <param name="message">The <see cref="InMemoryMessage"/> to schedule.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> instance to signal the request to cancel the operation.</param>
    /// <returns></returns>
    public async Task<long> ScheduleMessageAsync(InMemoryMessage message, CancellationToken cancellationToken = default)
    {
        await SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        return message.SequenceNumber;
    }

    /// <summary>
    /// Schedules a set of messages to appear on the transport at a later time.
    /// </summary>
    /// <param name="messages">The messages to schedule.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> instance to signal the request to cancel the operation.</param>
    /// <returns></returns>
    public async Task<IReadOnlyList<long>> ScheduleMessagesAsync(IEnumerable<InMemoryMessage> messages, CancellationToken cancellationToken = default)
    {
        await SendMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
        return messages.Select(m => m.SequenceNumber).ToArray();
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
        await updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // get matching
            var matching = items.SingleOrDefault(m => m.SequenceNumber == sequenceNumber)
                        ?? throw new ArgumentException($"An item with the sequence number {sequenceNumber} does not exist.", nameof(sequenceNumber));

            // make new items and replace
            items = items.AsEnumerable().Except(new[] { matching }).ToList();
        }
        finally
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
        await updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // get matching
            var matching = new List<InMemoryMessage>();
            foreach (var sn in sequenceNumbers)
            {
                var item = items.SingleOrDefault(m => m.SequenceNumber == sn)
                        ?? throw new ArgumentException($"An item with the sequence number {sn} does not exist.", nameof(sn));
            }

            // make new items and replace
            items = items.AsEnumerable().Except(matching).ToList();
        }
        finally
        {
            updateLock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        stoppingCts.Cancel();
        writer.Complete();
    }
}
