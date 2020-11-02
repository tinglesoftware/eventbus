using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    /// <summary>
    /// A message serializer is responsible for serializing and deserializing an event.
    /// </summary>
    public interface IEventSerializer
    {
        /// <summary>
        /// Serialize an event into a stream of bytes.
        /// </summary>
        /// <typeparam name="TEvent">The event type to be serialized.</typeparam>
        /// <param name="stream">
        /// The stream to serialize to.
        /// (It must be writeable, i.e. <see cref="Stream.CanWrite"/> must be true).
        /// </param>
        /// <param name="context">The context of the event to be serialized.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SerializeAsync<TEvent>(Stream stream, EventContext<TEvent> context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deserialize an event from a stream of bytes.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
        /// </param>
        /// <param name="type">The type to be desserialized</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<EventContext> DeserializeAsync(Stream stream, Type eventType, CancellationToken cancellationToken = default);
    }
}
