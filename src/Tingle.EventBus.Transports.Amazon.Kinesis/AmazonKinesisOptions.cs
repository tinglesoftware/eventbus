using Amazon;
using Amazon.Runtime;
using Amazon.Kinesis;
using System;

namespace Tingle.EventBus.Transports.Amazon.Kinesis
{
    /// <summary>
    /// Options for configuring Amazon Kinesis based event bus.
    /// </summary>
    public class AmazonKinesisOptions
    {
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
    }
}
