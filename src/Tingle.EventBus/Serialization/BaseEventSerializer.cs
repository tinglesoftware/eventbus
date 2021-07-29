using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        ///
        protected static readonly IList<string> JsonContentTypes = new[] { "application/json", "text/json", };

        private readonly EventBus bus;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="optionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        protected BaseEventSerializer(EventBus bus, IOptionsMonitor<EventBusOptions> optionsAccessor, ILoggerFactory loggerFactory)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            OptionsAccessor = optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor));

            // Create a well-scoped logger
            var categoryName = $"{LogCategoryNames.Serializers}.Default"; // TODO: pull from the type name and tim EventSerializer and Serializer 
            Logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

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
            var envelope = await Deserialize2Async<T>(stream: stream, contentType: contentType, cancellationToken: cancellationToken);
            if (envelope is null) return null;

            // Create the context with the event and popuate common properties
            return CreateEventContext(bus, envelope, contentType);
        }

        /// <inheritdoc/>
        public abstract Task<MessageEnvelope<T>?> Deserialize2Async<T>(Stream stream,
                                                                       ContentType? contentType,
                                                                       CancellationToken cancellationToken = default) where T : class;

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

            // Serialize
            var envelope = CreateMessageEnvelope(context);
            await SerializeAsync(stream: stream, envelope: envelope, cancellationToken: cancellationToken);
        }

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
        /// <returns></returns>
        protected MessageEnvelope<T> CreateMessageEnvelope<T>(EventContext<T> context)
        {
            var hostInfo = OptionsAccessor.CurrentValue.HostInfo;
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
