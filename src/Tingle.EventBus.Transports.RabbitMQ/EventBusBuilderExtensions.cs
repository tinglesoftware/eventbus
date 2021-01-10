using Microsoft.Extensions.Options;
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
        public static EventBusBuilder AddRabbitMqTransport(this EventBusBuilder builder, Action<RabbitMqTransportOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for RabbitMQ
            services.Configure(configure);
            services.AddSingleton<IPostConfigureOptions<RabbitMqTransportOptions>, RabbitMqPostConfigureOptions>();

            // The consumer names must be forced in RabbitMQ
            builder.Configure(options => options.ForceConsumerName = true);

            // register the transport
            builder.RegisterTransport<RabbitMqTransport, RabbitMqTransportOptions>();

            return builder;
        }
    }
}
