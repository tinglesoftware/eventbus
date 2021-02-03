using Microsoft.Extensions.Options;
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
            services.AddSingleton<IPostConfigureOptions<AmazonSqsTransportOptions>, AmazonSqsPostConfigureOptions>();

            // register the transport
            builder.AddTransport<AmazonSqsTransport, AmazonSqsTransportOptions>();

            return builder;
        }
    }
}
