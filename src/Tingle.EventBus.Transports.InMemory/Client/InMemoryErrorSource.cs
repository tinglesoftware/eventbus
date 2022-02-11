namespace Tingle.EventBus.Transports.InMemory.Client;

/// <summary>
/// The source of the error when <see cref="ProcessErrorEventArgs"/> is raised.
/// </summary>
public enum InMemoryErrorSource
{
    /// <summary>Message receive operation.</summary>
    Receive,
}
