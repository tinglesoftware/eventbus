using Amazon;
using Amazon.Kinesis;
using Amazon.Runtime;
using System;
using Tingle.EventBus;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Amazon Kinesis based event bus.
    /// </summary>
    public class AmazonKinesisTransportOptions : EventBusTransportOptionsBase
    {
        /// <inheritdoc/>
        public override EntityTypePreference DefaultEntityType { get; set; } = EntityTypePreference.Stream;

        /// <summary>
        /// The system name of the region to connect to.
        /// For example <c>eu-west-1</c>.
        /// When not configured, <see cref="Region"/> must be provided.
        /// </summary>
        public string RegionName { get; set; }

        /// <summary>
        /// The region to connect to.
        /// When not set, <see cref="RegionName"/> is used to set it.
        /// </summary>
        public RegionEndpoint Region { get; set; }

        /// <summary>
        /// The name of the key granted the requisite access control rights.
        /// </summary>
        public string AccessKey { get; set; }

        /// <summary>
        /// The secret associated with the <see cref="AccessKey"/>.
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Credentials for accessing AWS services.
        /// </summary>
        internal AWSCredentials Credentials { get; set; }

        /// <summary>
        /// Configuration for Kinesis
        /// </summary>
        public AmazonKinesisConfig KinesisConfig { get; set; }

        /// <summary>
        /// A function for selecting the partition key from an event context.
        /// This is called for event event before publishing.
        /// Defaults function uses <see cref="EventContext.Id"/> as the partion key.
        /// The value returned is hashed to determine the shard the event is sent to.
        /// </summary>
        public Func<EventContext, string> PartitionKeyResolver { get; set; } = (ctx) => ctx.Id;
    }
}
