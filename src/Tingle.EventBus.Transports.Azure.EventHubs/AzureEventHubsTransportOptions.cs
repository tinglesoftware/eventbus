﻿using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;
using System;
using Tingle.EventBus;
using Tingle.EventBus.Registrations;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure EventHubs based event bus.
    /// </summary>
    public class AzureEventHubsTransportOptions : AzureTransportOptions<AzureEventHubsTransportCredentials>
    {
        /// <inheritdoc/>
        public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Broadcast;

        /// <summary>
        /// Authentication credentials for Azure Blob Storage.
        /// This can either be a connection string or <see cref="AzureBlobStorageCredenetial"/>.
        /// Azure Blob Storage is used by the EventHubs processor to store the events stream offset
        /// which allows the events to be processed from a certain point.
        /// It is also used to create a lease per partition hence preventing duplicate events.
        /// </summary>
        public AnyOf<AzureBlobStorageCredenetial, string> BlobStorageCredentials { get; set; }

        /// <summary>
        /// The name of the blob container used by the EventHubs processor.
        /// Azure Blob Storage is used by the EventHubs processor to store the events stream offset
        /// which allows the events to be processed from a certain point.
        /// It is also used to create a lease per partition hence preventing duplicate events.
        /// Defaults to <c>checkpoints</c>
        /// </summary>
        public string BlobContainerName { get; set; } = "checkpoints";

        /// <summary>
        /// The type of transport to use.
        /// Defaults to <see cref="EventHubsTransportType.AmqpTcp"/>.
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

        /// <summary>
        /// A function to create the producer options instead of using the default options.
        /// Some options set may still be overriding for proper operation of the the transport and the bus.
        /// </summary>
        public Action<EventRegistration, EventHubProducerClientOptions> SetupProducerClientOptions { get; set; }

        /// <summary>
        /// A function to create the processor options instead of using the default options.
        /// Some options set may still be overriding for proper operation of the the transport and the bus.
        /// </summary>
        public Action<EventRegistration, EventConsumerRegistration, EventProcessorClientOptions> SetupProcessorClientOptions { get; set; }
    }
}
