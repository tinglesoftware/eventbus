using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus.Transports.Azure.EventHubs;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for Azure EventHubs
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add Azure EventHubs as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder, Action<AzureEventHubsOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Azure Service Bus
            services.Configure(configure);
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IPostConfigureOptions<AzureEventHubsOptions>, AzureEventHubsPostConfigureOptions>());

            // register the transport
            builder.RegisterTransport<AzureEventHubsTransport>();

            return builder;
        }

        /// <summary>
        /// Add Azure EventHubs as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="connectionString">
        /// The connection string to the Azure EventHubs namespace.
        /// Maps to <see cref="AzureEventHubsOptions.ConnectionString"/>.
        /// </param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureEventHubsTransport(this EventBusBuilder builder,
                                                                 string connectionString,
                                                                 Action<AzureEventHubsOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace", nameof(connectionString));
            }

            return builder.AddAzureEventHubsTransport(options =>
            {
                options.ConnectionString = connectionString;
                configure?.Invoke(options);
            });
        }
    }
}
