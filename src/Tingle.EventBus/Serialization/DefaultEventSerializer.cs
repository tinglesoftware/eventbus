using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// The default implementation of <see cref="IEventSerializer"/> that uses <c>System.Text.Json</c>.
    /// </summary>
    internal class DefaultEventSerializer : IEventSerializer
    {
        private static readonly ContentType JsonContentType = new(MediaTypeNames.Application.Json);

        private readonly EventBus bus;
        private readonly JsonSerializerOptions serializerOptions;
        private readonly ILogger logger;

        /// <summary>
        /// Creates an instance of <see cref="DefaultEventSerializer"/>.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="loggerFactory"></param>
        public DefaultEventSerializer(EventBus bus, IOptions<EventBusOptions> optionsAccessor, ILoggerFactory loggerFactory)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            serializerOptions = optionsAccessor?.Value?.SerializerOptions ?? throw new ArgumentNullException(nameof(optionsAccessor));

            // Create a well-scoped logger
            var categoryName = $"{LogCategoryNames.Serializers}.Default";
            logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public async Task SerializeAsync<T>(Stream stream,
                                            EventContext<T> context,
                                            HostInfo hostInfo,
                                            CancellationToken cancellationToken = default)
             where T : class
        {
            // Assume JSON content if not specified
            context.ContentType ??= JsonContentType;

            // Ensure the content type is supported
            if (!string.Equals(context.ContentType.MediaType, JsonContentType.MediaType))
            {
                throw new NotSupportedException($"The ContentType '{context.ContentType}' is not supported by this serializer");
            }

            // Create the envelope and popuate properties
            var envelope = new MessageEnvelope
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
            await JsonSerializer.SerializeAsync(utf8Json: stream,
                                                value: envelope,
                                                options: serializerOptions,
                                                cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<EventContext<T>> DeserializeAsync<T>(Stream stream,
                                                               ContentType contentType,
                                                               CancellationToken cancellationToken = default)
            where T : class
        {
            // Assume JSON content if not specified
            contentType ??= JsonContentType;

            // Ensure the content type is supported
            if (!string.Equals(contentType.MediaType, JsonContentType.MediaType))
            {
                throw new NotSupportedException($"The ContentType '{contentType}' is not supported by this serializer");
            }

            // Deserialize
            var envelope = await JsonSerializer.DeserializeAsync<MessageEnvelope>(utf8Json: stream,
                                                                                  options: serializerOptions,
                                                                                  cancellationToken: cancellationToken);

            // Ensure we have a JsonElement for the event
            if (!(envelope.Event is JsonElement eventToken) || eventToken.ValueKind == JsonValueKind.Null)
            {
                logger.LogWarning("The Event node is not a JsonElement or it is null");
                eventToken = new JsonElement();
            }

            // Get the event from the element
            T @event = typeof(T) == typeof(JsonElement) ? eventToken as T : ToObject<T>(eventToken, serializerOptions);

            // Create the context with the event and popuate common properties
            var context = new EventContext<T>(bus)
            {
                Id = envelope.Id,
                RequestId = envelope.RequestId,
                CorrelationId = envelope.CorrelationId,
                InitiatorId = envelope.InitiatorId,
                Expires = envelope.Expires,
                Sent = envelope.Sent,
                Headers = envelope.Headers,
                Event = @event,
                ContentType = contentType,
            };

            return context;
        }

        private static T ToObject<T>(JsonElement element, JsonSerializerOptions options = null)
        {
            var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                element.WriteTo(writer);
            }

            return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options);
        }
    }
}
