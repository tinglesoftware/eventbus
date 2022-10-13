using Amazon.Kinesis;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Options for configuring Amazon Kinesis based event bus.
/// </summary>
public class AmazonKinesisTransportOptions : AmazonTransportOptions
{
    /// <inheritdoc/>
    public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Broadcast;

    /// <summary>
    /// Configuration for Kinesis
    /// </summary>
    public AmazonKinesisConfig? KinesisConfig { get; set; }

    /// <summary>
    /// A function for selecting the partition key from an event context.
    /// This is called for event before publishing.
    /// Defaults function uses <see cref="EventContext.Id"/> as the partition key.
    /// The value returned is hashed to determine the shard the event is sent to.
    /// </summary>
    public Func<EventContext, string?> PartitionKeyResolver { get; set; } = (ctx) => ctx.Id;
}
