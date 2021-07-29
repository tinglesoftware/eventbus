using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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
    internal class DefaultEventSerializer : AbstractEventSerializer
    {
        /// <summary>
        /// Creates an instance of <see cref="DefaultEventSerializer"/>.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="loggerFactory"></param>
        public DefaultEventSerializer(IServiceProvider serviceProvider,
                                      IOptionsMonitor<EventBusOptions> optionsAccessor,
                                      ILoggerFactory loggerFactory)
            : base(serviceProvider, optionsAccessor, loggerFactory) { }

        /// <inheritdoc/>
        protected override IList<string> SupportedMediaTypes => JsonContentTypes;

        /// <inheritdoc/>
        protected override async Task<MessageEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                ContentType? contentType,
                                                                                CancellationToken cancellationToken = default) where T : class
        {
            var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
            return await JsonSerializer.DeserializeAsync<MessageEnvelope<T>>(utf8Json: stream,
                                                                             options: serializerOptions,
                                                                             cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task SerializeEnvelopeAsync<T>(Stream stream,
                                                        MessageEnvelope<T> envelope,
                                                        CancellationToken cancellationToken = default)
        {
            var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
            await JsonSerializer.SerializeAsync(utf8Json: stream,
                                                value: envelope,
                                                options: serializerOptions,
                                                cancellationToken: cancellationToken);
        }
    }
}
