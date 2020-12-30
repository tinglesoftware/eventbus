using Confluent.Kafka;
using System;
using System.Linq;
using Tingle.EventBus.Transports.Kafka;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for Kafka.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add Kafka as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddKafka(this EventBusBuilder builder, Action<KafkaOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Kafka
            services.Configure(configure);
            services.PostConfigure<KafkaOptions>(options =>
            {
                if (options.BootstrapServers == null && options.AdminConfig == null)
                {
                    throw new InvalidOperationException($"Either '{nameof(options.BootstrapServers)}' or '{nameof(options.AdminConfig)}' must be provided");
                }

                if (options.BootstrapServers != null && options.BootstrapServers.Any(b => string.IsNullOrWhiteSpace(b)))
                {
                    throw new ArgumentNullException(nameof(options.BootstrapServers), "A bootstrap server cannot be null or empty");
                }

                // ensure we have a config
                options.AdminConfig ??= new AdminClientConfig
                {
                    BootstrapServers = string.Join(",", options.BootstrapServers)
                };

                if (string.IsNullOrWhiteSpace(options.AdminConfig.BootstrapServers))
                {
                    throw new InvalidOperationException($"BootstrapServers must be provided via '{nameof(options.BootstrapServers)}' or '{nameof(options.AdminConfig)}'.");
                }

                // ensure the checkpoint interval is not less than 1
                options.CheckpointInterval = Math.Max(options.CheckpointInterval, 1);
            });

            // register the transport
            builder.RegisterTransport<KafkaTransport>();

            return builder;
        }
    }
}
