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
        ///
        protected static readonly IList<string> JsonContentTypes = new[] { "application/json", "text/json", };

        private static readonly Regex trimPattern = new("(Serializer|EventSerializer)$", RegexOptions.Compiled);

        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="optionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        protected AbstractEventSerializer(IServiceProvider serviceProvider, IOptionsMonitor<EventBusOptions> optionsAccessor, ILoggerFactory loggerFactory)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
        public async Task<EventContext<T>?> DeserializeAsync<T>(DeserializationContext context, CancellationToken cancellationToken = default)
            where T : class
        {
            // Assume first media type if none is specified
            var contentType = context.ContentType ?? new ContentType(SupportedMediaTypes[0]);

            // Ensure the content type is supported
            if (!SupportedMediaTypes.Contains(contentType.MediaType, StringComparer.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"The ContentType '{contentType}' is not supported by this serializer");
            }

            // Deserialize
            using var stream = context.Body.ToStream();
            var envelope = await DeserializeToEnvelopeAsync<T>(stream: stream, contentType: contentType, cancellationToken: cancellationToken);
            if (envelope is null)
            {
                Logger.DeserializationResultedInNull(context);
                return null;
            }

            if (envelope.Event is null)
            {
                Logger.DeserializedEventShouldNotBeNull(context, eventId: envelope.Id);
                return null;
            }

            // Create the context
            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();
            return new EventContext<T>(publisher: publisher, envelope: envelope, contentType: contentType, transportIdentifier: context.Identifier);
        }

        /// <inheritdoc/>
        public async Task SerializeAsync<T>(SerializationContext<T> context,
                                            CancellationToken cancellationToken = default)
             where T : class
        {
            // Assume first media type if none is specified
            context.Event.ContentType ??= new ContentType(SupportedMediaTypes[0]);

            // Ensure the content type is supported
            if (!SupportedMediaTypes.Contains(context.Event.ContentType.MediaType, StringComparer.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"The ContentType '{context.Event.ContentType}' is not supported by this serializer");
            }

            // Create the envelope for the event
            var hostInfo = OptionsAccessor.CurrentValue.HostInfo;
            var @event = context.Event;
            var envelope = new EventEnvelope<T>
            {
                Id = @event.Id,
                RequestId = @event.RequestId,
                CorrelationId = @event.CorrelationId,
                InitiatorId = @event.InitiatorId,
                Event = @event.Event,
                Expires = @event.Expires,
                Sent = @event.Sent,
                Headers = @event.Headers,
                Host = hostInfo,
            };

            // Serialize
            var stream = new MemoryStream();
            await SerializeEnvelopeAsync(stream: stream, envelope: envelope, cancellationToken: cancellationToken);

            // Return to the begining of the stream
            stream.Seek(0, SeekOrigin.Begin);

            context.Body = await BinaryData.FromStreamAsync(stream, cancellationToken);
        }

        /// <summary>
        /// Deserialize a stream of bytes to a <see cref="EventEnvelope{T}"/>.
        /// </summary>
        /// <typeparam name="T">The event type to be deserialized.</typeparam>
        /// <param name="stream">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
        /// </param>
        /// <param name="contentType">The type of content contained in the <paramref name="stream"/>.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task<EventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                 ContentType? contentType,
                                                                                 CancellationToken cancellationToken = default)
            where T : class;

        /// <summary>
        /// Serialize a <see cref="EventEnvelope{T}"/> into a stream of bytes.
        /// </summary>
        /// <param name="stream">
        /// The stream to serialize to.
        /// (It must be writeable, i.e. <see cref="Stream.CanWrite"/> must be true).
        /// </param>
        /// <param name="envelope">The <see cref="EventEnvelope{T}"/> to be serialized.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task SerializeEnvelopeAsync<T>(Stream stream,
                                                          EventEnvelope<T> envelope,
                                                          CancellationToken cancellationToken = default)
            where T : class;
    }
}
