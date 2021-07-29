using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
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
        private static readonly ContentType JsonContentType = new(MediaTypeNames.Application.Json);

        private readonly EventBus bus;
        private readonly JsonSerializer serializer;

        /// <summary>
        /// Creates an instance of <see cref="NewtonsoftJsonSerializer"/>.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="loggerFactory"></param>
        public NewtonsoftJsonSerializer(EventBus bus,
                                        IOptions<NewtonsoftJsonSerializerOptions> optionsAccessor,
                                        ILoggerFactory loggerFactory) : base(loggerFactory)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            var settings = optionsAccessor?.Value?.SerializerSettings ?? throw new ArgumentNullException(nameof(optionsAccessor));
            serializer = JsonSerializer.Create(settings);
        }

        /// <inheritdoc/>
        public override async Task<EventContext<T>?> DeserializeAsync<T>(Stream stream,
                                                                         ContentType? contentType,
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
            var envelope = await Deserialize2Async<T>(stream: stream, contentType: contentType, cancellationToken: cancellationToken);
            if (envelope is null) return null;

            // Create the context with the event and popuate common properties
            return CreateEventContext(bus, envelope, contentType);
        }

        /// <inheritdoc/>
        public override async Task SerializeAsync<T>(Stream stream,
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

            // Serialize
            var envelope = CreateMessageEnvelope(context, hostInfo);
            await SerializeAsync(stream: stream, envelope: envelope, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<MessageEnvelope<T>?> Deserialize2Async<T>(Stream stream, ContentType? contentType, CancellationToken cancellationToken = default)
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
        public override Task SerializeAsync(Stream stream, MessageEnvelope envelope, CancellationToken cancellationToken = default)
        {
            // Serialize
            using var sw = new StreamWriter(stream);
            using var jw = new JsonTextWriter(sw);
            serializer.Serialize(jw, envelope);

            return Task.CompletedTask;
        }
    }
}
