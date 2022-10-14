using Tingle.EventBus.Transports.Azure.QueueStorage;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Azure Queue Storage.
/// </summary>
public static class AzureQueueStorageEventBusBuilderExtensions
{
    /// <summary>Add Azure Queue Storage transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureQueueStorageTransport(this EventBusBuilder builder, Action<AzureQueueStorageTransportOptions>? configure = null)
        => builder.AddAzureQueueStorageTransport(AzureQueueStorageDefaults.Name, configure);

    /// <summary>Add Azure Queue Storage transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureQueueStorageTransport(this EventBusBuilder builder, string name, Action<AzureQueueStorageTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<AzureQueueStorageConfigureOptions>();
        return builder.AddTransport<AzureQueueStorageTransportOptions, AzureQueueStorageTransport>(name, configure);
    }
}
