using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
    public class NewtonsoftJsonSerializer : BaseEventSerializer
    {
        private readonly JsonSerializer serializer;

        /// <summary>
        /// Creates an instance of <see cref="NewtonsoftJsonSerializer"/>.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="serializerOptionsAccessor">The options for configuring the serializer.</param>
        /// <param name="optionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public NewtonsoftJsonSerializer(EventBus bus,
                                        IOptions<NewtonsoftJsonSerializerOptions> serializerOptionsAccessor,
                                        IOptionsMonitor<EventBusOptions> optionsAccessor,
                                        ILoggerFactory loggerFactory)
            : base(bus, optionsAccessor, loggerFactory)
        {
            var settings = serializerOptionsAccessor?.Value?.SerializerSettings ?? throw new ArgumentNullException(nameof(serializerOptionsAccessor));
            serializer = JsonSerializer.Create(settings);
        }

        /// <inheritdoc/>
        protected override IList<string> SupportedMediaTypes => JsonContentTypes;

        /// <inheritdoc/>
        protected override Task<MessageEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                          ContentType? contentType,
                                                                          CancellationToken cancellationToken = default) where T : class
        {
            // get the encoding and always default to UTF-8
            var encoding = Encoding.GetEncoding(contentType?.CharSet ?? Encoding.UTF8.BodyName);

            // Deserialize
            using var sr = new StreamReader(stream, encoding);
            using var jr = new JsonTextReader(sr);
            var envelope = serializer.Deserialize<MessageEnvelope<T>>(jr);
            return Task.FromResult(envelope);
        }

        /// <inheritdoc/>
        protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                  MessageEnvelope<T> envelope,
                                                  CancellationToken cancellationToken = default)
        {
            // Serialize
            using var sw = new StreamWriter(stream);
            using var jw = new JsonTextWriter(sw);
            serializer.Serialize(jw, envelope);

            return Task.CompletedTask;
        }
    }
}
