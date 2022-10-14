using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.Azure.QueueStorage;

/// <summary>Defaults for <see cref="AzureQueueStorageTransportOptions"/>.</summary>
public static class AzureQueueStorageDefaults
{
    /// <summary>Default name for <see cref="AzureQueueStorageTransportOptions"/>.</summary>
    public const string Name = "azure-queue-storage";
}
