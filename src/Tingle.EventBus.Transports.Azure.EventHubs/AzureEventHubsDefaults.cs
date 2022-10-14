using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.Azure.EventHubs;

/// <summary>Defaults for <see cref="AzureEventHubsTransportOptions"/>.</summary>
public static class AzureEventHubsDefaults
{
    /// <summary>Default name for <see cref="AzureEventHubsTransportOptions"/>.</summary>
    public const string Name = "azure-event-hubs";
}
