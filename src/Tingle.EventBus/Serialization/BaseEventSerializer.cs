using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// Abstract implementation for an event serializer.
    /// </summary>
    public abstract class BaseEventSerializer : IEventSerializer
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="loggerFactory"></param>
        protected BaseEventSerializer(ILoggerFactory loggerFactory)
        {
            // Create a well-scoped logger
            var categoryName = $"{LogCategoryNames.Serializers}.Default";
            Logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        protected ILogger Logger { get; }

        /// <inheritdoc/>
        public abstract Task<EventContext<T>?> DeserializeAsync<T>(Stream stream,
                                                                   ContentType? contentType,
                                                                   CancellationToken cancellationToken = default) where T : class;

        /// <inheritdoc/>
        public abstract Task<MessageEnvelope<T>?> Deserialize2Async<T>(Stream stream,
                                                                       ContentType? contentType,
                                                                       CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task SerializeAsync<T>(Stream stream,
                                               EventContext<T> context,
                                               HostInfo? hostInfo,
                                               CancellationToken cancellationToken = default) where T : class;

        /// <inheritdoc/>
        public abstract Task SerializeAsync(Stream stream, MessageEnvelope envelope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create an <see cref="EventContext{T}"/> from a <see cref="MessageEnvelope{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of event carried.</typeparam>
        /// <param name="bus"><see cref="EventBus"/> to be carried by the <see cref="EventContext{T}"/>.</param>
        /// <param name="envelope"><see cref="MessageEnvelope{T}"/> to use as the source.</param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        protected EventContext<T> CreateEventContext<T>(EventBus bus, MessageEnvelope<T> envelope, ContentType? contentType)
        {
            return new EventContext<T>(bus)
            {
                Id = envelope.Id,
                RequestId = envelope.RequestId,
                CorrelationId = envelope.CorrelationId,
                InitiatorId = envelope.InitiatorId,
                Event = envelope.Event,
                Expires = envelope.Expires,
                Sent = envelope.Sent,
                Headers = envelope.Headers,
                ContentType = contentType,
            };
        }

        /// <summary>
        /// Create a <see cref="MessageEnvelope{T}"/> from a <see cref="EventContext{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of event carried.</typeparam>
        /// <param name="context"><see cref="EventContext{T}"/> to use as the source.</param>
        /// <param name="hostInfo">Information about the host currently running this.</param>
        /// <returns></returns>
        protected MessageEnvelope<T> CreateMessageEnvelope<T>(EventContext<T> context, HostInfo? hostInfo) // TODO: remove HostInfo and get it from options
        {
            return new MessageEnvelope<T>
            {
                Id = context.Id,
                RequestId = context.RequestId,
                CorrelationId = context.CorrelationId,
                InitiatorId = context.InitiatorId,
                Event = context.Event,
                Expires = context.Expires,
                Sent = context.Sent,
                Headers = context.Headers,
                Host = hostInfo,
            };
        }
    }
}
