using RabbitMQ.Client;
using System;
using Tingle.EventBus.Transports.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for RabbitMQ.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add RabbitMQ as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddRabbitMq(this EventBusBuilder builder, Action<RabbitMqOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for RabbitMQ
            services.Configure(configure);
            services.PostConfigure<RabbitMqOptions>(options =>
            {
                // if we do not have a connection factory, attempt to create one
                if (options.ConnectionFactory == null)
                {
                    // ensure we have a hostname
                    if (string.IsNullOrWhiteSpace(options.Hostname))
                    {
                        throw new ArgumentNullException(nameof(options.Hostname), "The hostname is required to connect to a RabbitMQ broker");
                    }

                    // ensure we have a username and password
                    options.Username ??= "guest";
                    options.Password ??= "guest";

                    options.ConnectionFactory = new ConnectionFactory
                    {
                        HostName = options.Hostname,
                        UserName = options.Username,
                        Password = options.Password,
                    };
                }

                // at this point we have a connection factory, ensure certain settings are what we need them to be
                options.ConnectionFactory.DispatchConsumersAsync = true;

                // ensure the retries are not less than zero
                options.RetryCount = Math.Max(options.RetryCount, 0);
            });

            // The consumer names must be forced in RabbitMQ
            builder.Configure(options => options.ForceConsumerName = true);

            // register the transport
            builder.RegisterTransport<RabbitMqTransport>();

            return builder;
        }
    }
}
