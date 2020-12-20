using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// The default implementation of <see cref="IEventSerializer"/> that uses <c>Newtonsoft.Json</c>.
    /// </summary>
    internal class NewtonsoftJsonEventSerializer : IEventSerializer
    {
        private readonly Encoding encoding = Encoding.UTF8;
        private readonly JsonSerializer serializer;
        private readonly JsonSerializerSettings settings;

        /// <summary>
        /// Creates an instance of <see cref="NewtonsoftJsonEventSerializer"/>.
        /// </summary>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        public NewtonsoftJsonEventSerializer(IOptions<EventBusOptions> optionsAccessor)
        {
            var options = optionsAccessor?.Value?.SerializerOptions ?? throw new ArgumentNullException(nameof(optionsAccessor));

            settings = new JsonSerializerSettings()
            {
                NullValueHandling = options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
            };

            serializer = JsonSerializer.Create(settings);
        }

        /// <inheritdoc/>
        public ContentType ContentType => new ContentType("application/json; charset=utf-8");

        /// <inheritdoc/>
        public async Task SerializeAsync<T>(Stream stream,
                                            EventContext<T> context,
                                            HostInfo hostInfo,
                                            CancellationToken cancellationToken = default)
             where T : class
        {
            var envelope = new MessageEnvelope
            {
                EventId = context.EventId,
                RequestId = context.RequestId,
                ConversationId = context.ConversationId,
                CorrelationId = context.CorrelationId,
                InitiatorId = context.InitiatorId,
                Event = context.Event,
                Expires = context.Expires,
                Sent = context.Sent,
                Headers = context.Headers,
                Host = hostInfo,
            };

            using var sw = new StreamWriter(stream: stream, encoding: encoding, bufferSize: 1024, leaveOpen: true);
            using var jw = new JsonTextWriter(textWriter: sw);
            serializer.Serialize(jsonWriter: jw, value: envelope, objectType: typeof(MessageEnvelope));
            await jw.FlushAsync(cancellationToken: cancellationToken);
            await sw.FlushAsync();
        }

        /// <inheritdoc/>
        public Task<EventContext<T>> DeserializeAsync<T>(Stream stream, ContentType contentType, CancellationToken cancellationToken = default)
            where T : class
        {
            using var sr = new StreamReader(stream, encoding);
            using var jr = new JsonTextReader(sr);
            var envelope = serializer.Deserialize<MessageEnvelope>(jr);

            // ensure we have a JToken for the event
            if (!(envelope.Event is JToken eventToken) || eventToken.Type == JTokenType.Null)
            {
                eventToken = new JObject();
            }

            // get the event from the token
            T @event = default;
            if (typeof(T) == typeof(JToken)) @event = eventToken as T;
            else
            {
                using var jr_evt = eventToken.CreateReader();
                @event = serializer.Deserialize<T>(jr_evt);
            }

            // create the context with the event and popuate common properties
            var context = new EventContext<T>
            {
                EventId = envelope.EventId,
                RequestId = envelope.RequestId,
                ConversationId = envelope.ConversationId,
                CorrelationId = envelope.CorrelationId,
                InitiatorId = envelope.InitiatorId,
                Expires = envelope.Expires,
                Sent = envelope.Sent,
                Headers = envelope.Headers,
                Event = @event,
            };

            return Task.FromResult(context);
        }
    }
}
