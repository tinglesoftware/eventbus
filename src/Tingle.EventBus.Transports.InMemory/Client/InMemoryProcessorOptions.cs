namespace Tingle.EventBus.Transports.InMemory;

internal class InMemoryProcessorOptions
{
    public InMemoryProcessorSubQueue SubQueue { get; set; } = InMemoryProcessorSubQueue.None;
}

internal enum InMemoryProcessorSubQueue
{
    None,
    DeadLetter,
}
