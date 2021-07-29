using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// The default implementation of <see cref="IEventSerializer"/> for XML.
    /// </summary>
    public class DefaultXmlEventSerializer : AbstractEventSerializer
    {
        /// <summary>
        /// Creates an instance of <see cref="DefaultXmlEventSerializer"/>.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="loggerFactory"></param>
        public DefaultXmlEventSerializer(IServiceProvider serviceProvider,
                                         IOptionsMonitor<EventBusOptions> optionsAccessor,
                                         ILoggerFactory loggerFactory)
            : base(serviceProvider, optionsAccessor, loggerFactory) { }

        /// <inheritdoc/>
        protected override IList<string> SupportedMediaTypes => new[] { "application/xml", "text/xml", };

        /// <inheritdoc/>
        protected override Task<EventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                 ContentType? contentType,
                                                                                 CancellationToken cancellationToken = default)
        {
            var serializer = new XmlSerializer(typeof(EventEnvelope<T>));
            var envelope = (EventEnvelope<T>?)serializer.Deserialize(stream);
            return Task.FromResult(envelope);
        }

        /// <inheritdoc/>
        protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                          EventEnvelope<T> envelope,
                                                          CancellationToken cancellationToken = default)
        {
            var serializer = new XmlSerializer(typeof(EventEnvelope<T>));
            serializer.Serialize(stream, envelope);
            return Task.CompletedTask;
        }
    }
}
