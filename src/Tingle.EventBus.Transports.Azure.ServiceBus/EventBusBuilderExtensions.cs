using Microsoft.Azure.ServiceBus;
using System;
using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.ServiceBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for Azure Service Bus.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add Azure Service Bus as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureServiceBus(this EventBusBuilder builder, Action<AzureServiceBusOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Azure Service Bus
            services.Configure(configure);
            services.PostConfigure<AzureServiceBusOptions>(options =>
            {
                // ensure the connection string is not null
                if (string.IsNullOrWhiteSpace(options.ConnectionString) && options.ConnectionStringBuilder == null)
                {
                    throw new InvalidOperationException($"Either '{nameof(options.ConnectionString)}' or '{nameof(options.ConnectionStringBuilder)}' must be provided");
                }

                if (options.ConnectionStringBuilder == null)
                {
                    options.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder(options.ConnectionString)
                    {
                        TransportType = options.TransportType
                    };
                }
            });

            // register the event bus
            services.AddSingleton<IEventBus, AzureServiceBusEventBus>();

            return builder;
        }
    }
}
