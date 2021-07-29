﻿using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// A message serializer is responsible for serializing and deserializing an event.
    /// </summary>
    public interface IEventSerializer
    {
        /// <summary>
        /// Serialize an event into a stream of bytes.
        /// </summary>
        /// <typeparam name="T">The event type to be serialized.</typeparam>
        /// <param name="stream">
        /// The stream to serialize to.
        /// (It must be writeable, i.e. <see cref="Stream.CanWrite"/> must be true).
        /// </param>
        /// <param name="context">The context of the event to be serialized.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SerializeAsync<T>(Stream stream,
                               EventContext<T> context,
                               CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Deserialize an event from a stream of bytes.
        /// </summary>
        /// <typeparam name="T">The event type to be deserialized.</typeparam>
        /// <param name="stream">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
        /// </param>
        /// <param name="contentType">The type of content contained in the <paramref name="stream"/>.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<EventContext<T>?> DeserializeAsync<T>(Stream stream,
                                                   ContentType? contentType,
                                                   CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Serialize an event into a stream of bytes.
        /// </summary>
        /// <param name="stream">
        /// The stream to serialize to.
        /// (It must be writeable, i.e. <see cref="Stream.CanWrite"/> must be true).
        /// </param>
        /// <param name="envelope">The envelope to be serialized.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SerializeAsync(Stream stream,
                            MessageEnvelope envelope,
                            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deserialize an event from a stream of bytes.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
        /// </param>
        /// <param name="contentType">The type of content contained in the <paramref name="stream"/>.</param>
        /// <returns></returns>
        /// <param name="cancellationToken"></param>
        Task<MessageEnvelope<T>?> Deserialize2Async<T>(Stream stream,
                                                       ContentType? contentType,
                                                       CancellationToken cancellationToken = default) where T : class;
    }
}
