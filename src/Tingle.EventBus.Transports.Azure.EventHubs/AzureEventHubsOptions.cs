using Azure.Messaging.EventHubs;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure EventHubs based event bus.
    /// </summary>
    public class AzureEventHubsOptions
    {
        /// <summary>
        /// The connection string to Azure EventHubs.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The type of transport to use.
        /// Defaults to <see cref="EventHubsTransportType.AmqpTcp"/>
        /// </summary>
        public EventHubsTransportType TransportType { get; set; } = EventHubsTransportType.AmqpTcp;
    }
}
