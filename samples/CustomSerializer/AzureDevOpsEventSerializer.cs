﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;
using Tingle.EventBus.Serialization;

namespace CustomSerializer
{
    public class AzureDevOpsEventSerializer : IEventSerializer
    {
        private static readonly ContentType JsonContentType = new ContentType(MediaTypeNames.Application.Json);

        private readonly JsonSerializer serializer = JsonSerializer.CreateDefault();
        private readonly EventBus bus;

        public AzureDevOpsEventSerializer(EventBus bus)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <inheritdoc/>
        public Task<EventContext<T>> DeserializeAsync<T>(Stream stream,
                                                         ContentType contentType,
                                                         CancellationToken cancellationToken = default) where T : class
        {
            if (typeof(T) != typeof(AzureDevOpsCodePushed))
            {
                throw new InvalidOperationException($"Only '{nameof(AzureDevOpsCodePushed)}' events are supported.");
            }

            // Assume JSON content if not specified
            contentType ??= JsonContentType;

            // Ensure the content type is supported
            if (!contentType.MediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only JSON content is supported");
            }

            using var sr = new StreamReader(stream);
            using var jtr = new JsonTextReader(sr);
            var jToken = serializer.Deserialize<JToken>(jtr);

            var @event = jToken.ToObject<AzureDevOpsCodePushed>();
            var context = new EventContext<T>(bus)
            {
                Id = jToken.Value<string>("id"),
                Event = @event as T,
                Sent = jToken.Value<DateTime>("createdDate"),
            };

            // you can consider moving this to extenion methods on EventContext for both get and set
            context.Headers["eventType"] = jToken.Value<string>("eventType");
            context.Headers["resourceVersion"] = jToken.Value<string>("resourceVersion");
            context.Headers["publisherId"] = jToken.Value<string>("publisherId");

            return Task.FromResult(context);
        }

        /// <inheritdoc/>
        public Task SerializeAsync<T>(Stream stream,
                                      EventContext<T> context,
                                      HostInfo hostInfo,
                                      CancellationToken cancellationToken = default) where T : class
        {
            throw new NotSupportedException("Serialization of AzureDevOps events should never happen.");
        }
    }
}
