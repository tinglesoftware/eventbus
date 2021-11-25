namespace Tingle.EventBus.Transports;

/// <summary>
/// Represents the result from consuming an event.
/// This only used by the transport(s) and should not be used by the final application.
/// </summary>
public struct EventConsumeResult
{
    /// <summary>
    /// Create an instance of <see cref="EventConsumeResult"/>
    /// </summary>
    /// <param name="successful">See <see cref="Successful"/>.</param>
    /// <param name="exception">See <see cref="Exception"/>.</param>
    internal EventConsumeResult(bool successful, Exception? exception = null)
    {
        Successful = successful;
        Exception = exception;
    }

    /// <summary>
    /// Indicates if the consuming action was successful.
    /// </summary>
    public bool Successful { get; }

    /// <summary>
    /// Exception caught for failures.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Deconstruct the result into parts
    /// </summary>
    /// <param name="successful">See <see cref="Successful"/>.</param>
    /// <param name="exception">See <see cref="Exception"/>.</param>
    public void Deconstruct(out bool successful, out Exception? exception)
    {
        successful = Successful;
        exception = Exception;
    }
}
