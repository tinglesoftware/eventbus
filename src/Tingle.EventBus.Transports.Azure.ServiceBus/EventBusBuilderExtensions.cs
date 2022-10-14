using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.ServiceBus;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Azure Service Bus.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Add Azure Service Bus as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureServiceBusTransport(this EventBusBuilder builder, Action<AzureServiceBusTransportOptions>? configure = null)
        => builder.AddAzureServiceBusTransport(TransportNames.AzureServiceBus, configure);

    /// <summary>
    /// Add Azure Service Bus as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureServiceBusTransport(this EventBusBuilder builder, string name, Action<AzureServiceBusTransportOptions> configure)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var services = builder.Services;

        // configure the options for Azure Service Bus
        services.Configure(configure);
        services.ConfigureOptions<AzureServiceBusConfigureOptions>();

        // register the transport
        builder.AddTransport<AzureServiceBusTransport, AzureServiceBusTransportOptions>(name);

        return builder;
    }
}
