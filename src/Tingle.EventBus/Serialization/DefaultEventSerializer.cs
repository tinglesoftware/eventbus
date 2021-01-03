using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// The default implementation of <see cref="IEventSerializer"/> that uses <c>Newtonsoft.Json</c>.
    /// </summary>
    internal class DefaultEventSerializer : IEventSerializer
    {
        private static readonly ContentType JsonContentType = new ContentType("application/json; charset=utf-8");

        private readonly EventBus bus;
        private readonly JsonSerializerOptions serializerOptions;

        /// <summary>
        /// Creates an instance of <see cref="DefaultEventSerializer"/>.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        public DefaultEventSerializer(EventBus bus, IOptions<EventBusOptions> optionsAccessor)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            serializerOptions = optionsAccessor?.Value?.SerializerOptions ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        /// <inheritdoc/>
        public async Task<ContentType> SerializeAsync<T>(Stream stream,
                                                         EventContext<T> context,
                                                         HostInfo hostInfo,
                                                         CancellationToken cancellationToken = default)
             where T : class
        {
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

            await JsonSerializer.SerializeAsync(utf8Json: stream,
                                                value: envelope,
                                                options: serializerOptions,
                                                cancellationToken: cancellationToken);

            return JsonContentType;
        }

        /// <inheritdoc/>
        public async Task<EventContext<T>> DeserializeAsync<T>(Stream stream,
                                                               ContentType contentType,
                                                               CancellationToken cancellationToken = default)
            where T : class
        {
            var envelope = await JsonSerializer.DeserializeAsync<MessageEnvelope>(utf8Json: stream,
                                                                                  options: serializerOptions,
                                                                                  cancellationToken: cancellationToken);

            // ensure we have a JsonElement for the event
            if (!(envelope.Event is JsonElement eventToken) || eventToken.ValueKind == JsonValueKind.Null)
            {
                eventToken = new JsonElement();
            }

            // get the event from the element
            T @event = typeof(T) == typeof(JsonElement) ? eventToken as T : ToObject<T>(eventToken, serializerOptions);

            // create the context with the event and popuate common properties
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
