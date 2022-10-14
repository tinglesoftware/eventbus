namespace Tingle.EventBus.Transports;

/// <summary>TransportRegistrations assign a name to a specific <see cref="IEventBusTransport"/>
/// transportType.
/// </summary>
public class TransportRegistration
{
    /// <summary>Initializes a new instance of <see cref="TransportRegistration"/>.</summary>
    /// <param name="name">The name for the transport.</param>
    /// <param name="transportType">The <see cref="IEventBusTransport"/> type that is registered.</param>
    public TransportRegistration(string name, Type transportType) : this(name, null, transportType) { }

    /// <summary>Initializes a new instance of <see cref="TransportRegistration"/>.</summary>
    /// <param name="name">The name for the transport.</param>
    /// <param name="displayName">The display name for the transport.</param>
    /// <param name="transportType">The <see cref="IEventBusTransport"/> type that is registered.</param>
    public TransportRegistration(string name, string? displayName, Type transportType)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TransportType = transportType ?? throw new ArgumentNullException(nameof(transportType));
        DisplayName = displayName;

        if (!typeof(IEventBusTransport).IsAssignableFrom(transportType))
        {
            throw new ArgumentException("transportType must implement IEventBusTransport.");
        }
    }

    /// <summary>The name of the transport.// </summary>
    public string Name { get; }

    /// <summary>The display name for the transport. (Optional)</summary>
    public string? DisplayName { get; }

    /// <summary>The <see cref="IEventBusTransport"/> type that is registered.</summary>
    public Type TransportType { get; }
}
