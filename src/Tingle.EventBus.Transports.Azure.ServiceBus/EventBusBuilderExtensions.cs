using Microsoft.Extensions.Options;
using System;
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
        public static EventBusBuilder AddAzureServiceBusTransport(this EventBusBuilder builder, Action<AzureServiceBusTransportOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Azure Service Bus
            services.Configure(configure);
            services.AddSingleton<IPostConfigureOptions<AzureServiceBusTransportOptions>, AzureServiceBusPostConfigureOptions>();

            // register the transport
            builder.AddTransport<AzureServiceBusTransport, AzureServiceBusTransportOptions>();

            return builder;
        }

        /// <summary>
        /// Add Azure Service Bus as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="connectionString">
        /// The connection string to the Azure Service Bus namespace.
        /// </param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureServiceBusTransport(this EventBusBuilder builder,
                                                                  string connectionString,
                                                                  Action<AzureServiceBusTransportOptions>? configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace", nameof(connectionString));
            }

            return builder.AddAzureServiceBusTransport(options =>
            {
                options.Credentials = connectionString;
                configure?.Invoke(options);
            });
        }
    }
}
