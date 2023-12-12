namespace Tingle.EventBus.Transports.InMemory.Client;

/// <summary>
/// The <see cref="ProcessMessageEventArgs"/> contain event args that are specific
/// to the <see cref="InMemoryReceivedMessage"/> that is being processed.
/// </summary>
/// <param name="message">The message to be processed.</param>
/// <param name="cancellationToken">
/// The processor's <see cref="CancellationToken"/> which will be cancelled
/// in the event that <see cref="InMemoryProcessor.StopProcessingAsync(CancellationToken)"/> is called.
/// </param>
internal class ProcessMessageEventArgs(InMemoryReceivedMessage message, CancellationToken cancellationToken) : EventArgs
{
    /// <summary>The received message to be processed.</summary>
    public InMemoryReceivedMessage Message { get; } = message;

    /// <summary>
    /// The processor's <see cref="CancellationToken"/> instance which will be cancelled
    /// when <see cref="InMemoryProcessor.StopProcessingAsync(CancellationToken)"/> is called.
    /// </summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;
}
