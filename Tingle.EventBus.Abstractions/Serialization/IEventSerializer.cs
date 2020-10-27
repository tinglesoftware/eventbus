using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public interface IEventSerializer
    {
        /// <summary>
        /// Convert an event to a <see cref="Stream"/>. 
        /// The caller will take ownership of the stream and ensure it is correctly disposed of.
        /// </summary>
        /// <typeparam name="TEvent">The event type to be serialized.</typeparam>
        /// <param name="event">The event to be serialized</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A readable Stream containing JSON of the serialized object.</returns>
        /// <remarks>
        /// Implementations of this function must result in a <see cref="Stream"/> which can be read.
        /// (i.e. <see cref="Stream.CanRead"/> must be true).
        /// See <see href="https://docs.microsoft.com/dotnet/api/system.io.stream.canread?view=netcore-2.0"/>
        /// for more information on readable streams.
        /// </remarks>
        Task<MemoryStream> ToStreamAsync<TEvent>(TEvent @event, Encoding encoding, CancellationToken cancellationToken = default);

        /// <summary>
        /// Convert a <see cref="Stream"/> of bytes to an object. 
        /// The implementation is responsible for Disposing of the stream,
        /// including when an exception is thrown, to avoid memory leaks.
        /// </summary>
        /// <typeparam name="TEvent">The event type to be deserialized.</typeparam>
        /// <param name="stream">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It stream must be readable, i.e. <see cref="Stream.CanRead"/> must be true)
        /// </param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TEvent> FromStreamAsync<TEvent>(MemoryStream stream, Encoding encoding, CancellationToken cancellationToken = default);

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
