using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using System;
using Tingle.EventBus.Transports.Amazon.Sqs;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for Amazon SQS.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add Amazon SQS as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAmazonSqsTransport(this EventBusBuilder builder, Action<AmazonSqsTransportOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Amazon SQS and SNS option
            services.Configure(configure);
            services.PostConfigure<AmazonSqsTransportOptions>(options =>
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

                // ensure we have options for SQS and SNS and their regions are set
                options.SqsConfig ??= new AmazonSQSConfig();
                options.SqsConfig.RegionEndpoint ??= options.Region;
                options.SnsConfig ??= new AmazonSimpleNotificationServiceConfig();
                options.SnsConfig.RegionEndpoint ??= options.Region;
            });

            // register the transport
            builder.AddTransport<AmazonSqsTransport, AmazonSqsTransportOptions>();

            return builder;
        }
    }
}
