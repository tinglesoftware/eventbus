using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.Azure.ServiceBus;

/// <summary>Defaults for <see cref="AzureServiceBusTransportOptions"/>.</summary>
public static class AzureServiceBusDefaults
{
    /// <summary>Default name for <see cref="AzureServiceBusTransportOptions"/>.</summary>
    public const string Name = "azure-service-bus";
}
