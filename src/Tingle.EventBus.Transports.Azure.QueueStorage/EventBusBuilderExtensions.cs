using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.QueueStorage;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Azure Queue Storage.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Add Azure Queue Storage as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureQueueStorageTransport(this EventBusBuilder builder, Action<AzureQueueStorageTransportOptions> configure)
    {
        return builder.AddAzureQueueStorageTransport(TransportNames.AzureQueueStorage, configure);
    }

    /// <summary>
    /// Add Azure Queue Storage as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureQueueStorageTransport(this EventBusBuilder builder, string name, Action<AzureQueueStorageTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var services = builder.Services;

        // configure the options for Azure Queue Storage
        services.Configure(configure);
        services.ConfigureOptions<AzureQueueStorageConfigureOptions>();

        // register the transport
        builder.AddTransport<AzureQueueStorageTransport, AzureQueueStorageTransportOptions>(name);

        return builder;
    }
}
