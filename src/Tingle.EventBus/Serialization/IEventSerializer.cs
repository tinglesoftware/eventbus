using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// A message serializer is responsible for serializing and deserializing an event.
/// </summary>
public interface IEventSerializer
{
    /// <summary>Serialize an event into a stream of bytes.</summary>
    /// <typeparam name="T">The event type to be serialized.</typeparam>
    /// <param name="context">The <see cref="SerializationContext{T}"/> to use.</param>
    /// <param name="cancellationToken"></param>
    Task SerializeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(SerializationContext<T> context, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Deserialize an event from a stream of bytes.</summary>
    /// <typeparam name="T">The event type to be deserialized.</typeparam>
    /// <param name="context">The <see cref="DeserializationContext"/> to use.</param>
    /// <param name="cancellationToken"></param>
    Task<IEventEnvelope<T>?> DeserializeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(DeserializationContext context, CancellationToken cancellationToken = default) where T : class;
}
