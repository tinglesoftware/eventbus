using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryProcessor // TODO: handle disposing
    {
        private readonly ChannelReader<InMemoryMessage> reader;
        private CancellationTokenSource? stoppingCts = new();

        public InMemoryProcessor(ChannelReader<InMemoryMessage> reader)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var messages = reader.ReadAllAsync(cancellationToken);
            await foreach (var message in messages)
            {
                var rm = new InMemoryReceivedMessage(message);
                var args = new ProcessMessageEventArgs(rm, cancellationToken);
                _ = ProcessMessageAsync!.Invoke(args);
            }
        }

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

            // TODO: connect to the channel here?
            stoppingCts = new CancellationTokenSource();
            _ = RunAsync(stoppingCts.Token);
            return Task.CompletedTask;
        }

        ///
        public Task StopProcessingAsync(CancellationToken cancellationToken = default)
        {
            stoppingCts?.Cancel();
            stoppingCts = null;
            return Task.CompletedTask;
        }
    }
}
