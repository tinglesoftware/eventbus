using Tingle.EventBus.Transports.Azure.EventHubs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Azure EventHubs
/// </summary>
public static class AzureEventHubsEventBusBuilderExtensions
{
    /// <summary>Add Azure EventHubs transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder, Action<AzureEventHubsTransportOptions>? configure = null)
        => builder.AddAzureEventHubsTransport(AzureEventHubsDefaults.Name, configure);

    /// <summary>Add Azure EventHubs transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder, string name, Action<AzureEventHubsTransportOptions>? configure = null)
        => builder.AddTransport<AzureEventHubsTransport, AzureEventHubsTransportOptions, AzureEventHubsConfigureOptions>(name, configure);
}
