﻿using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Used to build <see cref="EventBusTransportRegistration"/>s.</summary>
/// <param name="name">The name of the transport being built.</param>
public class EventBusTransportRegistrationBuilder(string name)
{
    /// <summary>Gets the name of the transport being built.</summary>
    public string Name { get; } = name;

    /// <summary>Gets or sets the display name for the transport being built.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the <see cref="IEventBusTransport"/> type responsible for this transport.</summary>
    [DynamicallyAccessedMembers(TrimmingHelper.Transport)]
    public Type? TransportType { get; set; }

    /// <summary>Builds the <see cref="EventBusTransportRegistration"/> instance.</summary>
    /// <returns>The <see cref="EventBusTransportRegistration"/>.</returns>
    public EventBusTransportRegistration Build()
    {
        if (TransportType is null)
        {
            throw new InvalidOperationException($"{nameof(TransportType)} must be configured to build an {nameof(EventBusTransportRegistration)}.");
        }

        return new EventBusTransportRegistration(Name, DisplayName, TransportType);
    }
}
