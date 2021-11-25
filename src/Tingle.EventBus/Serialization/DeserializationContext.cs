using System;
using System.Net.Mime;
using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// Context for performing deserialization.
/// </summary>
public sealed class DeserializationContext
{
    /// <summary>Creates and instance of <see cref="DeserializationContext"/>.</summary>
    /// <param name="serviceProvider">The provider to use to resolve any required services in the given scope.</param>
    /// <param name="body">The <see cref="BinaryData"/> containing the raw data.</param>
    /// <param name="registration">Registration for this event being deserialized.</param>
    /// <param name="identifier">Identifier given by the transport for the event to be deserialized.</param>
    public DeserializationContext(IServiceProvider serviceProvider, BinaryData body, EventRegistration registration, string? identifier = null)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        Identifier = identifier;
    }

    /// <summary>
    /// The provider to use to resolve any required services in the given scope.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

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
    public ContentType? ContentType { get; set; }
}
