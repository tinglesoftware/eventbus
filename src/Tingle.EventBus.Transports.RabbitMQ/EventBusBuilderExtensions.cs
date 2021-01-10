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

            // register the transport
            builder.AddTransport<RabbitMqTransport, RabbitMqTransportOptions>();

            return builder;
        }
    }
}
