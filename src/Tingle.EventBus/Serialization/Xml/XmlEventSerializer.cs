﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Serialization.Xml;

/// <summary>
/// The default implementation of <see cref="IEventSerializer"/> for XML.
/// </summary>
/// <param name="optionsAccessor">The options for configuring the serializer.</param>
/// <param name="loggerFactory"></param>
[RequiresUnreferencedCode(MessageStrings.XmlSerializationUnreferencedCodeMessage)]
public class XmlEventSerializer(IOptionsMonitor<EventBusSerializationOptions> optionsAccessor, ILoggerFactory loggerFactory)
    : AbstractEventSerializer(optionsAccessor, loggerFactory)
{
    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => ["application/xml", "text/xml"];

    /// <inheritdoc/>
    protected override Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(
        Stream stream,
        DeserializationContext context,
        CancellationToken cancellationToken = default)
    {
        var serializer = new XmlSerializer(typeof(XmlEventEnvelope<T>));
        var envelope = (XmlEventEnvelope<T>?)serializer.Deserialize(stream);
        return Task.FromResult<IEventEnvelope<T>?>(envelope);
    }

    /// <inheritdoc/>
    protected override Task SerializeEnvelopeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(
        Stream stream,
        EventEnvelope<T> envelope,
        CancellationToken cancellationToken = default)
    {
        var serializer = new XmlSerializer(typeof(XmlEventEnvelope<T>));
        serializer.Serialize(stream, new XmlEventEnvelope<T>(envelope));
        return Task.CompletedTask;
    }
}
