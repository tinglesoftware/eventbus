﻿using Microsoft.Extensions.DependencyInjection;
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
using Tingle.EventBus.Serialization;

namespace CustomEventSerializer
{
    public class AzureDevOpsEventSerializer : AbstractEventSerializer
    {
        private readonly JsonSerializer serializer = JsonSerializer.CreateDefault();

        public AzureDevOpsEventSerializer(IServiceProvider serviceProvider,
                                          IOptionsMonitor<EventBusOptions> optionsAccessor,
                                          ILoggerFactory loggerFactory)
            : base(serviceProvider, optionsAccessor, loggerFactory) { }

        /// <inheritdoc/>
        protected override IList<string> SupportedMediaTypes => JsonContentTypes;

        /// <inheritdoc/>
        protected override Task<EventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                 ContentType? contentType,
                                                                                 CancellationToken cancellationToken = default)
        {
            using var sr = new StreamReader(stream);
            using var jtr = new JsonTextReader(sr);
            var jToken = serializer.Deserialize<JToken>(jtr);

            if (jToken is null) return Task.FromResult<EventEnvelope<T>?>(null);

            var @event = jToken.ToObject<AzureDevOpsCodePushed>();
            var envelope = new EventEnvelope<T>
            {
                Id = jToken.Value<string>("id"),
                Event = @event as T,
                Sent = jToken.Value<DateTime>("createdDate"),
            };

            // you can consider moving this to extenion methods on EventContext for both get and set
            envelope.Headers["eventType"] = jToken.Value<string>("eventType")!;
            envelope.Headers["resourceVersion"] = jToken.Value<string>("resourceVersion")!;
            envelope.Headers["publisherId"] = jToken.Value<string>("publisherId")!;

            return Task.FromResult<EventEnvelope<T>?>(envelope);
        }

        /// <inheritdoc/>
        protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                          EventEnvelope<T> envelope,
                                                          CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Serialization of AzureDevOps events should never happen.");
        }
    }
}