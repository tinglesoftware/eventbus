using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Tests.Configurator;

internal class FakeEventSerializer1 : IEventSerializer
{
    /// <inheritdoc/>
    public Task<IEventEnvelope<T>?> DeserializeAsync<T>(DeserializationContext context, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task SerializeAsync<T>(SerializationContext<T> context, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException();
    }
}
