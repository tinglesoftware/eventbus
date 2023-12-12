using System.Threading.Channels;

namespace Tingle.EventBus.Transports.InMemory.Client;

internal class BroadcastChannelWriter<T>(ICollection<ChannelWriter<T>>? children = null) : ChannelWriter<T>
{
    public ICollection<ChannelWriter<T>> Children { get; } = children ?? new List<ChannelWriter<T>>();

    /// <inheritdoc/>
    public override bool TryWrite(T item) => Children.All(w => w.TryWrite(item));

    /// <inheritdoc/>
    public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Waiting is not supported.");
    }
}
