using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports;

/// <summary>TransportRegistrations assign a name to a specific <see cref="IEventBusTransport"/>
/// transportType.
/// </summary>
public class EventBusTransportRegistration
{
    /// <summary>Initializes a new instance of <see cref="EventBusTransportRegistration"/>.</summary>
    /// <param name="name">The name for the transport.</param>
    /// <param name="transportType">The <see cref="IEventBusTransport"/> type that is registered.</param>
    public EventBusTransportRegistration(string name, [DynamicallyAccessedMembers(TrimmingHelper.Transport)] Type transportType) : this(name, null, transportType) { }

    /// <summary>Initializes a new instance of <see cref="EventBusTransportRegistration"/>.</summary>
    /// <param name="name">The name for the transport.</param>
    /// <param name="displayName">The display name for the transport.</param>
    /// <param name="transportType">The <see cref="IEventBusTransport"/> type that is registered.</param>
    public EventBusTransportRegistration(string name, string? displayName, [DynamicallyAccessedMembers(TrimmingHelper.Transport)] Type transportType)
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
    [DynamicallyAccessedMembers(TrimmingHelper.Transport)]
    public Type TransportType { get; }
}
