namespace Tingle.EventBus.Transports.InMemory.Client;

/// <summary>
/// Contains information about the entity whose processing threw an exception, as
/// well as the exception that has been thrown.
/// </summary>
/// <param name="exception">The exception that triggered the call to the error event handler.</param>
/// <param name="errorSource">The source associated with the error.</param>
/// <param name="entityPath">The entity path used when this exception occurred.</param>
/// <param name="cancellationToken">
/// The processor's <see cref="CancellationToken"/> instance which will be cancelled
/// in the event that <see cref="InMemoryProcessor.StopProcessingAsync(CancellationToken)"/> is called.
/// </param>
internal class ProcessErrorEventArgs(Exception exception, InMemoryErrorSource errorSource, string entityPath, CancellationToken cancellationToken) : EventArgs
{
    /// <summary>
    /// Gets the exception that triggered the call to the error event handler.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the source associated with the error.
    /// </summary>
    public InMemoryErrorSource ErrorSource { get; } = errorSource;

    /// <summary>
    /// Gets the entity path associated with the error event.
    /// </summary>
    public string EntityPath { get; } = entityPath;

    /// <summary>
    /// Gets the processor's <see cref="CancellationToken"/> instance which will be
    /// cancelled when <see cref="InMemoryProcessor.StopProcessingAsync(CancellationToken)"/>
    /// is called.
    /// </summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;
}
