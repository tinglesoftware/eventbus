using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure Queue Storage based event bus.
    /// </summary>
    public class AzureQueueStorageTransportOptions : AzureTransportOptions
    {
        /// <inheritdoc/>
        public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Queue;

        /// <summary>
        /// The connection string to Azure Queue Storage.
        /// </summary>
        public string ConnectionString { get; set; }
    }
}
