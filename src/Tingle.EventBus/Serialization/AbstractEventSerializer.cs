using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// Abstract implementation for an event serializer.
    /// </summary>
    public abstract class AbstractEventSerializer : IEventSerializer
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected static readonly IList<string> JsonContentTypes = new[] { "application/json", "text/json", };
        protected static readonly IList<string> XmlContentTypes = new[] { "application/xml", "text/xml", };
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        private static readonly Regex trimPattern = new("(Serializer|EventSerializer)$", RegexOptions.Compiled);

        private readonly EventBus bus;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="optionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        protected AbstractEventSerializer(EventBus bus, IOptionsMonitor<EventBusOptions> optionsAccessor, ILoggerFactory loggerFactory)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            OptionsAccessor = optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor));

            // Create a well-scoped logger
            Name = trimPattern.Replace(GetType().Name, "");
            var categoryName = $"{LogCategoryNames.Serializers}.{Name}";
            Logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        ///
        protected string Name { get; }

        ///
        protected abstract IList<string> SupportedMediaTypes { get; }

        ///
        protected IOptionsMonitor<EventBusOptions> OptionsAccessor { get; }

        ///
        protected ILogger Logger { get; }

        /// <inheritdoc/>
        public async Task<EventContext<T>?> DeserializeAsync<T>(Stream stream,
                                                                ContentType? contentType,
                                                                CancellationToken cancellationToken = default)
            where T : class
        {
            // Assume first media type if none is specified
            contentType ??= new ContentType(SupportedMediaTypes[0]);

            // Ensure the content type is supported
            if (!SupportedMediaTypes.Contains(contentType.MediaType, StringComparer.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"The ContentType '{contentType}' is not supported by this serializer");
            }

            // Deserialize
            var envelope = await DeserializeToEnvelopeAsync<T>(stream: stream, contentType: contentType, cancellationToken: cancellationToken);
            if (envelope is null) return null;

            // Create the context
            var context = new EventContext<T>(bus)
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
            return context;
        }

        /// <inheritdoc/>
        public async Task SerializeAsync<T>(Stream stream,
                                            EventContext<T> context,
                                            CancellationToken cancellationToken = default)
             where T : class
        {
            // Assume first media type if none is specified
            context.ContentType ??= new ContentType(SupportedMediaTypes[0]);

            // Ensure the content type is supported
            if (!SupportedMediaTypes.Contains(context.ContentType.MediaType, StringComparer.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"The ContentType '{context.ContentType}' is not supported by this serializer");
            }

            // Create the envelope for the event
            var hostInfo = OptionsAccessor.CurrentValue.HostInfo;
            var envelope = new MessageEnvelope<T>
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

            // Serialize
            await SerializeEnvelopeAsync(stream: stream, envelope: envelope, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Deserialize a stream of bytes to a <see cref="MessageEnvelope{T}"/>.
        /// </summary>
        /// <typeparam name="T">The event type to be deserialized.</typeparam>
        /// <param name="stream">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
        /// </param>
        /// <param name="contentType">The type of content contained in the <paramref name="stream"/>.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task<MessageEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                   ContentType? contentType,
                                                                                   CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Serialize a <see cref="MessageEnvelope{T}"/> into a stream of bytes.
        /// </summary>
        /// <param name="stream">
        /// The stream to serialize to.
        /// (It must be writeable, i.e. <see cref="Stream.CanWrite"/> must be true).
        /// </param>
        /// <param name="envelope">The <see cref="MessageEnvelope{T}"/> to be serialized.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task SerializeEnvelopeAsync<T>(Stream stream,
                                                          MessageEnvelope<T> envelope,
                                                          CancellationToken cancellationToken = default)
            where T : class;
    }
}
