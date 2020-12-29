using Azure.Messaging.ServiceBus;
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
                if (string.IsNullOrWhiteSpace(options.ConnectionString) && options.ConnectionStringProperties == null)
                {
                    throw new InvalidOperationException($"Either '{nameof(options.ConnectionString)}' or '{nameof(options.ConnectionStringProperties)}' must be provided");
                }

                if (options.ConnectionStringProperties == null)
                {
                    options.ConnectionStringProperties = ServiceBusConnectionStringProperties.Parse(options.ConnectionString);
                }
            });

            // register the event bus
            services.AddSingleton<IEventBus, AzureServiceBusEventBus>();

            return builder;
        }

        /// <summary>
        /// Add Azure Service Bus as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="connectionString">
        /// The connection string to the Azure Service Bus namespace.
        /// Maps to <see cref="AzureServiceBusOptions.ConnectionString"/>.
        /// </param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureServiceBus(this EventBusBuilder builder,
                                                         string connectionString,
                                                         Action<AzureServiceBusOptions> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace", nameof(connectionString));
            }

            return builder.AddAzureServiceBus(options =>
            {
                options.ConnectionString = connectionString;
                configure?.Invoke(options);
            });
        }
    }
}
