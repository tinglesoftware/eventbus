using Amazon;
using Amazon.Kinesis;
using System;
using Tingle.EventBus.Transports.Amazon.Kinesis;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for Amazon Kinesis.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add Amazon Kinesis as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAmazonKinesisTransport(this EventBusBuilder builder, Action<AmazonKinesisTransportOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // Configure the options for Amazon Kinesis
            services.Configure(configure);
            services.PostConfigure<AmazonKinesisTransportOptions>(options =>
            {
                // Ensure the region is provided
                if (string.IsNullOrWhiteSpace(options.RegionName) && options.Region == null)
                {
                    throw new InvalidOperationException($"Either '{nameof(options.RegionName)}' or '{nameof(options.Region)}' must be provided");
                }

                options.Region ??= RegionEndpoint.GetBySystemName(options.RegionName);

                // Ensure the access key is specified
                if (string.IsNullOrWhiteSpace(options.AccessKey))
                {
                    throw new InvalidOperationException($"The '{nameof(options.AccessKey)}' must be provided");
                }

                // Ensure the secret is specified
                if (string.IsNullOrWhiteSpace(options.SecretKey))
                {
                    throw new InvalidOperationException($"The '{nameof(options.SecretKey)}' must be provided");
                }

                // Ensure we have options for Kinesis and the region is set
                options.KinesisConfig ??= new AmazonKinesisConfig();
                options.KinesisConfig.RegionEndpoint ??= options.Region;

                // Ensure the partition key resolver is set
                if (options.PartitionKeyResolver == null)
                {
                    throw new InvalidOperationException($"The '{nameof(options.PartitionKeyResolver)}' must be provided");
                }
            });

            // Register the transport
            builder.AddTransport<AmazonKinesisTransport, AmazonKinesisTransportOptions>();

            return builder;
        }
    }
}
