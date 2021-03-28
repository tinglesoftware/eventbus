using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System;
using Tingle.EventBus;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Amazon SQS based event bus.
    /// </summary>
    public class AmazonSqsTransportOptions : EventBusTransportOptionsBase
    {
        /// <inheritdoc/>
        public override EntityTypePreference DefaultEntityType { get; set; } = EntityTypePreference.Queue;

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
        /// Configuration for SQS
        /// </summary>
        public AmazonSQSConfig SqsConfig { get; set; }

        /// <summary>
        /// Configuration for SNS
        /// </summary>
        public AmazonSimpleNotificationServiceConfig SnsConfig { get; set; }

        /// <summary>
        /// A setup function for setting up settings for a topic.
        /// This is only called before creation.
        /// </summary>
        public Action<CreateTopicRequest> SetupCreateTopicRequest { get; set; }

        /// <summary>
        /// A setup function for setting up settings for a queue.
        /// This is only called before creation.
        /// </summary>
        public Action<CreateQueueRequest> SetupCreateQueueRequest { get; set; }
    }
}
