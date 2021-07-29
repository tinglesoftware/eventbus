using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;
using Tingle.EventBus.Serialization;

namespace CustomSerializer
{
    public class AzureDevOpsEventSerializer : BaseEventSerializer
    {
        private readonly JsonSerializer serializer = JsonSerializer.CreateDefault();

        public AzureDevOpsEventSerializer(EventBus bus,
                                          IOptionsMonitor<EventBusOptions> optionsAccessor,
                                          ILoggerFactory loggerFactory)
            : base(bus, optionsAccessor, loggerFactory) { }

        /// <inheritdoc/>
        protected override IList<string> SupportedMediaTypes => JsonContentTypes;

        /// <inheritdoc/>
        protected override Task<MessageEnvelope<T>?> Deserialize2Async<T>(Stream stream,
                                                                          ContentType? contentType,
                                                                          CancellationToken cancellationToken = default) where T : class
        {
            using var sr = new StreamReader(stream);
            using var jtr = new JsonTextReader(sr);
            var jToken = serializer.Deserialize<JToken>(jtr);

            if (jToken is null) return Task.FromResult<MessageEnvelope<T>?>(null);

            var @event = jToken.ToObject<AzureDevOpsCodePushed>();
            var envelope = new MessageEnvelope<T>
            {
                Id = jToken.Value<string>("id"),
                Event = @event as T,
                Sent = jToken.Value<DateTime>("createdDate"),
            };

            // you can consider moving this to extenion methods on EventContext for both get and set
            envelope.Headers["eventType"] = jToken.Value<string>("eventType");
            envelope.Headers["resourceVersion"] = jToken.Value<string>("resourceVersion");
            envelope.Headers["publisherId"] = jToken.Value<string>("publisherId");

            return Task.FromResult<MessageEnvelope<T>?>(envelope);
        }

        /// <inheritdoc/>
        protected override Task SerializeAsync<T>(Stream stream,
                                                  MessageEnvelope<T> envelope,
                                                  CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Serialization of AzureDevOps events should never happen.");
        }
    }
}
