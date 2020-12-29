﻿using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;

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
        /// The connection string to Azure Blob Storage.
        /// Azure Blob Storage is used by the EventHubs processor to store the events stream offset
        /// which allows the events to be processed from a certain point.
        /// It is also used to create a lease per partition hence preventing duplicate events.
        /// </summary>
        public string BlobStorageConnectionString { get; set; }

        /// <summary>
        /// The prefix to use for blob containers used by the EventHubs processor.
        /// Azure Blob Storage is used by the EventHubs processor to store the events stream offset
        /// which allows the events to be processed from a certain point.
        /// It is also used to create a lease per partition hence preventing duplicate events.
        /// Defaults to <c>checkpoints-</c>
        /// </summary>
        public string BlobContainerPrefix { get; set; } = "checkpoints-";

        /// <summary>
        /// The type of transport to use.
        /// Defaults to <see cref="EventHubsTransportType.AmqpTcp"/>
        /// </summary>
        public EventHubsTransportType TransportType { get; set; } = EventHubsTransportType.AmqpTcp;

        /// <summary>
        /// Gets or sets value indicating if the Event Hubs namespace is in the Basic tier.
        /// The Basic tier does not support mutiple consumer groups.
        /// In this case, the transport would make use of the default consumer group only
        /// (<see cref="EventHubConsumerClient.DefaultConsumerGroupName"/>).
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool UseBasicTier { get; set; } = true;
    }
}
