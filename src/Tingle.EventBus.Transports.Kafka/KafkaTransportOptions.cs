using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Options for configuring Kafka based event bus.
/// </summary>
public class KafkaTransportOptions : EventBusTransportOptionsBase
{
    /// <inheritdoc/>
    public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Broadcast;

    /// <summary>
    /// The initial list of brokers indicated as <c>host</c> or <c>host:port</c>
    /// </summary>
    public List<string>? BootstrapServers { get; set; }

    /// <summary>
    /// Configuration for Admin Client
    /// </summary>
    public AdminClientConfig? AdminConfig { get; set; }

    /// <summary>
    /// A setup function for setting up specifications for a topic.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, TopicSpecification>? SetupTopicSpecification { get; set; }

    /// <summary>
    /// The number of events consumed after which to checkpoint.
    /// For values other than <c>1</c>, the implementations of
    /// <see cref="IEventConsumer{T}"/> for Kafka events must
    /// handle duplicate detection.
    /// </summary>
    /// <remarks>
    /// The Commit is done by sending a "commit offsets" request to the Kafka
    /// cluster and synchronously waits for the response. This can be very
    /// slow compared to the rate at which the consumer is capable of
    /// consuming messages. A high performance application will typically
    /// set the interval high so that commits are done relatively infrequently
    /// and be designed handle duplicate messages in the event of failure.
    /// </remarks>
    /// <value>Defaults to 1</value>
    public int CheckpointInterval { get; set; } = 1;
}
