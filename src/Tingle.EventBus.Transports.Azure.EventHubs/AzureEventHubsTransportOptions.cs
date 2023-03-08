using AnyOfTypes;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Options for configuring Azure EventHubs based event bus.
/// </summary>
public class AzureEventHubsTransportOptions : AzureTransportOptions<AzureEventHubsTransportCredentials>
{
    /// <inheritdoc/>
    public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Broadcast;

    /// <summary>
    /// Authentication credentials for Azure Blob Storage.
    /// This can either be a connection string or <see cref="AzureBlobStorageCredentials"/>.
    /// Azure Blob Storage is used by the EventHubs processor to store the events stream offset
    /// which allows the events to be processed from a certain point.
    /// It is also used to create a lease per partition hence preventing duplicate events.
    /// </summary>
    public AnyOf<AzureBlobStorageCredentials, string> BlobStorageCredentials { get; set; }

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
    /// The Basic tier does not support multiple consumer groups.
    /// In this case, the transport would make use of the default consumer group only
    /// (<see cref="EventHubConsumerClient.DefaultConsumerGroupName"/>).
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool UseBasicTier { get; set; } = true;

    /// <summary>
    /// The number of events consumed after which to checkpoint.
    /// For values other than <c>1</c>, the implementations of
    /// <see cref="IEventConsumer{T}"/> for Event Hub events must
    /// handle duplicate detection.
    /// </summary>
    /// <remarks>
    /// The checkpoint is done by writing to Azure Blob Storage.
    /// This can occasionally be slow compared to the rate at which the consumer
    /// is capable of consuming messages. A low checkpoint interval may increase
    /// the costs incurred by your Azure Blob Storage account.
    /// A high performance application will typically set the interval high so
    /// that checkpoints are done relatively infrequently and be designed to handle
    /// duplicate messages in the event of failure.
    /// </remarks>
    /// <value>Defaults to 1</value>
    public int CheckpointInterval { get; set; } = 1;

    /// <summary>
    /// A function to create <see cref="BlobClientOptions"/> used for the checkpoint store.
    /// Only specify if you need to change defaults.
    /// </summary>
    public Func<BlobClientOptions> SetupBlobClientOptions { get; set; } = () => new();

    /// <summary>
    /// A function for setting up options for a producer.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, EventHubProducerClientOptions>? SetupProducerClientOptions { get; set; }

    /// <summary>
    /// A function for setting up options for a processor.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, EventConsumerRegistration, EventProcessorClientOptions>? SetupProcessorClientOptions { get; set; }
}
