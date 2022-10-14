using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.EventHubs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Azure EventHubs
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Add Azure EventHubs as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder, Action<AzureEventHubsTransportOptions> configure)
        => builder.AddAzureEventHubsTransport(TransportNames.AzureEventHubs, configure);

    /// <summary>
    /// Add Azure EventHubs as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder, string name, Action<AzureEventHubsTransportOptions> configure)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var services = builder.Services;

        // configure the options for Azure Event Hubs
        services.Configure(configure);
        services.ConfigureOptions<AzureEventHubsConfigureOptions>();

        // register the transport
        builder.AddTransport<AzureEventHubsTransport, AzureEventHubsTransportOptions>(name);

        return builder;
    }
}
