using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Serializers
{
    /// <summary>
    /// Implementation of <see cref="IEventSerializer"/> that uses <c>Newtonsoft.Json</c>.
    /// </summary>
    public class NewtonsoftJsonSerializer : IEventSerializer
    {
        private static readonly ContentType JsonContentType = new(MediaTypeNames.Application.Json);

        private readonly EventBus bus;
        private readonly ILogger logger;
        private readonly JsonSerializer serializer;

        /// <summary>
        /// Creates an instance of <see cref="NewtonsoftJsonSerializer"/>.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="logger"></param>
        public NewtonsoftJsonSerializer(EventBus bus,
                                        IOptions<NewtonsoftJsonSerializerOptions> optionsAccessor,
                                        ILogger<NewtonsoftJsonSerializer> logger)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var settings = optionsAccessor?.Value?.SerializerSettings ?? throw new ArgumentNullException(nameof(optionsAccessor));
            serializer = JsonSerializer.Create(settings);
        }

        /// <inheritdoc/>
        public Task SerializeAsync<T>(Stream stream,
                                      EventContext<T> context,
                                      HostInfo? hostInfo,
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
            using var sw = new StreamWriter(stream);
            using var jw = new JsonTextWriter(sw);
            serializer.Serialize(jw, envelope);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<EventContext<T>> DeserializeAsync<T>(Stream stream,
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

            // get the encoding and always default to UTF-8
            var encoding = Encoding.GetEncoding(contentType?.CharSet ?? Encoding.UTF8.BodyName);

            // Deserialize
            using var sr = new StreamReader(stream, encoding);
            using var jr = new JsonTextReader(sr);
            var envelope = serializer.Deserialize<MessageEnvelope>(jr);

            // Ensure we have a JToken for the event
            if (envelope.Event is not JToken eventToken || eventToken.Type == JTokenType.Null)
            {
                logger.LogWarning("The Event node is not a JToken or it is null");
                eventToken = typeof(IEnumerable).IsAssignableFrom(typeof(T)) ? new JArray() : (JToken)new JObject();
            }

            // Get the event from the token
            T? @event = typeof(T) == typeof(JToken) ? eventToken as T : eventToken.ToObject<T>(serializer);

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

            return Task.FromResult<EventContext<T>?>(context);
        }
    }
}
