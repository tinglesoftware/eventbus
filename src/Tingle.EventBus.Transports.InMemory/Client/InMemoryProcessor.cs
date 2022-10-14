using System.Threading.Channels;

namespace Tingle.EventBus.Transports.InMemory.Client;

internal class InMemoryProcessor : IDisposable
{
    private readonly ChannelReader<InMemoryMessage> reader;
    private CancellationTokenSource stoppingCts = new();

    public InMemoryProcessor(string entityPath, ChannelReader<InMemoryMessage> reader)
    {
        if (string.IsNullOrWhiteSpace(EntityPath = entityPath))
        {
            throw new ArgumentException($"'{nameof(entityPath)}' cannot be null or whitespace.", nameof(entityPath));
        }

        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var messages = reader.ReadAllAsync(cancellationToken);
        await foreach (var message in messages.ConfigureAwait(false))
        {
            try
            {
                var rm = new InMemoryReceivedMessage(message);
                var args = new ProcessMessageEventArgs(rm, cancellationToken);
                _ = ProcessMessageAsync!.Invoke(args);
            }
            catch (Exception ex)
            {
                var args = new ProcessErrorEventArgs(ex, InMemoryErrorSource.Receive, EntityPath, cancellationToken);
                _ = ProcessErrorAsync!.Invoke(args);
            }
        }
    }

    public string EntityPath { get; }

    /// <summary>
    /// The handler responsible for processing unhandled exceptions thrown while this
    /// processor is running.
    /// Implementation is mandatory.
    /// </summary>
    public event Func<ProcessErrorEventArgs, Task>? ProcessErrorAsync;

    /// <summary>
    /// The handler responsible for processing messages received from the Queue or Subscription.
    /// Implementation is mandatory.
    /// </summary>
    public event Func<ProcessMessageEventArgs, Task>? ProcessMessageAsync;

    ///
    public Task CloseAsync(CancellationToken cancellationToken = default) => StopProcessingAsync(cancellationToken);

    ///
    public Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        if (ProcessMessageAsync is null)
        {
            throw new InvalidOperationException($"'{nameof(ProcessMessageAsync)}' should be set prior to start processing");
        }

        stoppingCts = new CancellationTokenSource();
        _ = RunAsync(stoppingCts.Token);
        return Task.CompletedTask;
    }

    ///
    public Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        stoppingCts.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        stoppingCts.Cancel();
    }
}
