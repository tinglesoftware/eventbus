using System.Net.Mime;
using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// Context for performing deserialization.
/// </summary>
public sealed class DeserializationContext
{
    /// <summary>Creates and instance of <see cref="DeserializationContext"/>.</summary>
    /// <param name="body">The <see cref="BinaryData"/> containing the raw data.</param>
    /// <param name="registration">Registration for this event being deserialized.</param>
    /// <param name="identifier">Identifier given by the transport for the event to be deserialized.</param>
    public DeserializationContext(BinaryData body, EventRegistration registration, string? identifier = null)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        Identifier = identifier;
    }

    /// <summary>
    /// The <see cref="BinaryData"/> containing the raw data.
    /// </summary>
    public BinaryData Body { get; }

    /// <summary>
    /// Registration for this event being deserialized.
    /// </summary>
    public EventRegistration Registration { get; }

    /// <summary>
    /// Identifier given by the transport for the event to be deserialized.
    /// </summary>
    public string? Identifier { get; }

    /// <summary>
    /// Type of content contained in the <see cref="Body"/>.
    /// </summary>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// The raw data provided by the transport without any manipulation.
    /// It can be null depending on the transport implementation.
    /// It is meant to be used by serializers who need context as to what the transport provided.
    /// <br/>
    /// For example for Event Hubs this is of type Azure.Messaging.EventHubs.EventData,
    /// whereas for Service Bus this is of type Azure.Messaging.ServiceBus.ServiceBusMessage.
    /// </summary>
    public object? RawTransportData { get; init; }
}
