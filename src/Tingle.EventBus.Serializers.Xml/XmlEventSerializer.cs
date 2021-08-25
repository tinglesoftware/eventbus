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
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Serializers
{
    /// <summary>
    /// The default implementation of <see cref="IEventSerializer"/> for XML.
    /// </summary>
    public class XmlEventSerializer : AbstractEventSerializer
    {
        /// <summary>
        /// Creates an instance of <see cref="XmlEventSerializer"/>.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="optionsAccessor">The options for configuring the serializer.</param>
        /// <param name="loggerFactory"></param>
        public XmlEventSerializer(IServiceProvider serviceProvider,
                                  IOptionsMonitor<EventBusOptions> optionsAccessor,
                                  ILoggerFactory loggerFactory)
            : base(serviceProvider, optionsAccessor, loggerFactory) { }

        /// <inheritdoc/>
        protected override IList<string> SupportedMediaTypes => new[] { "application/xml", "text/xml", };

        /// <inheritdoc/>
        protected override Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                  ContentType? contentType,
                                                                                  CancellationToken cancellationToken = default)
        {
            var serializer = new XmlSerializer(typeof(XmlEventEnvelope<T>));
            var envelope = (XmlEventEnvelope<T>?)serializer.Deserialize(stream);
            return Task.FromResult<IEventEnvelope<T>?>(envelope);
        }

        /// <inheritdoc/>
        protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                          EventEnvelope<T> envelope,
                                                          CancellationToken cancellationToken = default)
        {
            var serializer = new XmlSerializer(typeof(XmlEventEnvelope<T>));
            serializer.Serialize(stream, new XmlEventEnvelope<T>(envelope));
            return Task.CompletedTask;
        }
    }
}
