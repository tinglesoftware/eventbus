using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// The default implementation of <see cref="IEventSerializer"/> for JSON using the <c>System.Text.Json</c> library.
    /// </summary>
    public class DefaultJsonEventSerializer : AbstractEventSerializer
    {
        /// <summary>
        /// Creates an instance of <see cref="DefaultJsonEventSerializer"/>.
        /// </summary>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="loggerFactory"></param>
        public DefaultJsonEventSerializer(IOptionsMonitor<EventBusOptions> optionsAccessor,
                                          ILoggerFactory loggerFactory)
            : base(optionsAccessor, loggerFactory) { }

        /// <inheritdoc/>
        protected override IList<string> SupportedMediaTypes => JsonContentTypes;

        /// <inheritdoc/>
        protected override async Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                        ContentType? contentType,
                                                                                        CancellationToken cancellationToken = default)
        {
            var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
            return await JsonSerializer.DeserializeAsync<EventEnvelope<T>>(utf8Json: stream,
                                                                           options: serializerOptions,
                                                                           cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        protected override async Task SerializeEnvelopeAsync<T>(Stream stream,
                                                                EventEnvelope<T> envelope,
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
