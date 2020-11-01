using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions.Serialization
{
    /// <summary>
    /// The default implementation of <see cref="IEventSerializer"/> that uses <c>Newtonsoft.Json</c>.
    /// </summary>
    public class NewtonsoftJsonEventSerializer : IEventSerializer
    {
        private readonly Encoding encoding = Encoding.UTF8;
        private readonly HostInfo hostInfo;
        private readonly JsonSerializer serializer;
        private readonly JsonSerializerSettings settings;

        /// <summary>
        /// Creates an instance of <see cref="NewtonsoftJsonEventSerializer"/>.
        /// </summary>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        public NewtonsoftJsonEventSerializer(HostInfo hostInfo, IOptions<EventBusOptions> optionsAccessor)
        {
            this.hostInfo = hostInfo ?? throw new ArgumentNullException(nameof(hostInfo));
            var options = optionsAccessor?.Value?.SerializerOptions ?? throw new ArgumentNullException(nameof(optionsAccessor));

            settings = new JsonSerializerSettings()
            {
                NullValueHandling = options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
            };

            serializer = JsonSerializer.Create(settings);
        }

        /// <inheritdoc/>
        public Task<object> FromStreamAsync(MemoryStream stream, Type type, Encoding encoding, CancellationToken cancellationToken = default)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(type)) return Task.FromResult((object)stream);

                using var sr = new StreamReader(stream, encoding);
                using var jr = new JsonTextReader(sr);
                var result = serializer.Deserialize(jr, type);
                return Task.FromResult(result);
            }
        }

        /// <inheritdoc/>
        public async Task SerializeAsync<TEvent>(Stream stream, EventContext<TEvent> context, CancellationToken cancellationToken = default)
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
    }
}
