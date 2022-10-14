using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.EventHubs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Azure EventHubs
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>Add Azure EventHubs transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder, Action<AzureEventHubsTransportOptions>? configure = null)
        => builder.AddAzureEventHubsTransport(TransportNames.AzureEventHubs, configure);

    /// <summary>Add Azure EventHubs transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder, string name, Action<AzureEventHubsTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<AzureEventHubsConfigureOptions>();
        return builder.AddTransport<AzureEventHubsTransport, AzureEventHubsTransportOptions>(name, configure);
    }
}
