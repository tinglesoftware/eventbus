using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    /// The default implementation of <see cref="IEventSerializer"/> that uses <c>System.Text.Json</c>.
    /// </summary>
    internal class DefaultEventSerializer : BaseEventSerializer
    {
        private static readonly ContentType JsonContentType = new(MediaTypeNames.Application.Json);

        private readonly EventBus bus;
        private readonly JsonSerializerOptions serializerOptions;

        /// <summary>
        /// Creates an instance of <see cref="DefaultEventSerializer"/>.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="loggerFactory"></param>
        public DefaultEventSerializer(EventBus bus,
                                      IOptions<EventBusOptions> optionsAccessor,
                                      ILoggerFactory loggerFactory) : base(loggerFactory)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            serializerOptions = optionsAccessor?.Value?.SerializerOptions ?? throw new ArgumentNullException(nameof(optionsAccessor));
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
        public override async Task<MessageEnvelope<T>?> Deserialize2Async<T>(Stream stream,
                                                                             ContentType? contentType,
                                                                             CancellationToken cancellationToken = default)
        {
            return await JsonSerializer.DeserializeAsync<MessageEnvelope<T>>(utf8Json: stream,
                                                                             options: serializerOptions,
                                                                             cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task SerializeAsync(Stream stream, MessageEnvelope envelope, CancellationToken cancellationToken = default)
        {
            await JsonSerializer.SerializeAsync(utf8Json: stream,
                                                value: envelope,
                                                inputType: envelope.GetType(), // without this, the event property does not get serialized
                                                options: serializerOptions,
                                                cancellationToken: cancellationToken);
        }
    }
}
