using Amazon;
using Amazon.Kinesis;
using System;
using Tingle.EventBus;
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
        public static EventBusBuilder AddAmazonKinesis(this EventBusBuilder builder, Action<AmazonKinesisOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Amazon Kinesis
            services.Configure(configure);
            services.PostConfigure<AmazonKinesisOptions>(options =>
            {
                // ensure the region is provided
                if (string.IsNullOrWhiteSpace(options.RegionName) && options.Region == null)
                {
                    throw new InvalidOperationException($"Either '{nameof(options.RegionName)}' or '{nameof(options.Region)}' must be provided");
                }

                options.Region ??= RegionEndpoint.GetBySystemName(options.RegionName);

                // ensure the access key is specified
                if (string.IsNullOrWhiteSpace(options.AccessKey))
                {
                    throw new InvalidOperationException($"The '{nameof(options.AccessKey)}' must be provided");
                }

                // ensure the secret is specified
                if (string.IsNullOrWhiteSpace(options.SecretKey))
                {
                    throw new InvalidOperationException($"The '{nameof(options.SecretKey)}' must be provided");
                }

                // ensure we have options for Kinesis and the region is set
                options.KinesisConfig ??= new AmazonKinesisConfig();
                options.KinesisConfig.RegionEndpoint ??= options.Region;
            });

            // register the event bus
            services.AddSingleton<IEventBus, AmazonKinesisEventBus>();

            return builder;
        }
    }
}
