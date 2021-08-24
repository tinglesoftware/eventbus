using System.IO;
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
        /// <param name="context">The <see cref="DeserializationContext"/> to use.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<EventContext<T>?> DeserializeAsync<T>(DeserializationContext context,
                                                   CancellationToken cancellationToken = default) where T : class;
    }
}
