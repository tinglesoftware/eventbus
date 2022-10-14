using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Used to build <see cref="TransportRegistration"/>s.</summary>
public class EventBusTransportRegistrationBuilder
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="name">The name of the transport being built.</param>
    public EventBusTransportRegistrationBuilder(string name)
    {
        Name = name;
    }

    /// <summary>Gets the name of the transport being built.</summary>
    public string Name { get; }

    /// <summary>Gets or sets the display name for the transport being built.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the <see cref="IEventBusTransport"/> type responsible for this transport.</summary>
    public Type? TransportType { get; set; }

    /// <summary>Builds the <see cref="TransportRegistration"/> instance.</summary>
    /// <returns>The <see cref="TransportRegistration"/>.</returns>
    public TransportRegistration Build()
    {
        if (TransportType is null)
        {
            throw new InvalidOperationException($"{nameof(TransportType)} must be configured to build an {nameof(TransportRegistration)}.");
        }

        return new TransportRegistration(Name, DisplayName, TransportType);
    }
}
