using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.ServiceBus;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Azure Service Bus.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>Add Azure Service Bus transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureServiceBusTransport(this EventBusBuilder builder, Action<AzureServiceBusTransportOptions>? configure = null)
        => builder.AddAzureServiceBusTransport(TransportNames.AzureServiceBus, configure);

    /// <summary>Add Azure Service Bus transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddAzureServiceBusTransport(this EventBusBuilder builder, string name, Action<AzureServiceBusTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<AzureServiceBusConfigureOptions>();
        return builder.AddTransport<AzureServiceBusTransport, AzureServiceBusTransportOptions>(name, configure);
    }
}
