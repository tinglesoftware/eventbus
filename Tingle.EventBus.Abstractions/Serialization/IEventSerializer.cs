using System;
using System.IO;
using System.Text;
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
        /// Serialize and event into a stream of bytes.
        /// </summary>
        /// <typeparam name="TEvent">The event type to be serialized.</typeparam>
        /// <param name="stream">The stream to serialize to.</param>
        /// <param name="context">The context of the event to be serialized.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SerializeAsync<TEvent>(Stream stream, EventContext<TEvent> context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Convert a <see cref="Stream"/> of bytes to an object. 
        /// The implementation is responsible for Disposing of the stream,
        /// including when an exception is thrown, to avoid memory leaks.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It stream must be readable, i.e. <see cref="Stream.CanRead"/> must be true)
        /// </param>
        /// <param name="type">The type to be desserialized</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<object> FromStreamAsync(MemoryStream stream, Type type, Encoding encoding, CancellationToken cancellationToken = default);
    }
}
