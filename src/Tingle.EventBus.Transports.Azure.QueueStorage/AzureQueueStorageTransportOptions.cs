using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure Queue Storage based event bus.
    /// </summary>
    public class AzureQueueStorageTransportOptions : AzureTransportOptions<AzureQueueStorageTransportCredentials>
    {
        /// <inheritdoc/>
        public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Queue;
    }
}
