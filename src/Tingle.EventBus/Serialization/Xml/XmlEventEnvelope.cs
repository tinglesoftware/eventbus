﻿using System.Xml.Serialization;

namespace Tingle.EventBus.Serialization.Xml;

///
[XmlRoot("EventEnvelope")]
public class XmlEventEnvelope<T> : IEventEnvelope<T> where T : class
{
    ///
    public XmlEventEnvelope() { }

    ///
    public XmlEventEnvelope(EventEnvelope<T> envelope)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));

        Id = envelope.Id;
        RequestId = envelope.RequestId;
        CorrelationId = envelope.CorrelationId;
        InitiatorId = envelope.InitiatorId;
        Expires = envelope.Expires;
        Sent = envelope.Sent;
        Headers = envelope.Headers.Select(kvp => (XmlHeader)kvp).ToArray();
        Host = envelope.Host;
        Event = envelope.Event;
    }

    /// <inheritdoc/>
    public string? Id { get; set; }

    /// <inheritdoc/>
    public string? RequestId { get; set; }

    /// <inheritdoc/>
    public string? CorrelationId { get; set; }

    /// <inheritdoc/>
    public string? InitiatorId { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? Expires { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? Sent { get; set; }

    /// <inheritdoc/>
    [XmlIgnore]
    IDictionary<string, string> IEventEnvelope.Headers
    {
        get
        {
            var pairs = Headers.Select(h => (KeyValuePair<string, string>)h);
            return new Dictionary<string, string>(pairs, StringComparer.OrdinalIgnoreCase);
        }
    }

    ///
    public XmlHeader[] Headers { get; set; } = Array.Empty<XmlHeader>();

    /// <inheritdoc/>
    public HostInfo? Host { get; set; }

    /// <inheritdoc/>
    public T? Event { get; set; }
}
