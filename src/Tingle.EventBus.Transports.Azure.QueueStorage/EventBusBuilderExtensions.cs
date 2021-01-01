using System;
using Tingle.EventBus.Transports.Azure.QueueStorage;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for Azure Queue Storage.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add Azure Queue Storage as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureQueueStorageTransport(this EventBusBuilder builder, Action<AzureQueueStorageTransportOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Azure Queue Storage
            services.Configure(configure);
            services.PostConfigure<AzureQueueStorageTransportOptions>(options =>
            {
                // ensure the connection string
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    throw new InvalidOperationException($"The '{nameof(options.ConnectionString)}' must be provided");
                }
            });

            // register the transport
            builder.RegisterTransport<AzureQueueStorageTransport, AzureQueueStorageTransportOptions>();

            return builder;
        }

        /// <summary>
        /// Add Azure Queue Storage as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="connectionString">
        /// The connection string to the Azure Storage account.
        /// Maps to <see cref="AzureQueueStorageTransportOptions.ConnectionString"/>.
        /// </param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureQueueStorageTransport(this EventBusBuilder builder,
                                                                    string connectionString,
                                                                    Action<AzureQueueStorageTransportOptions> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace", nameof(connectionString));
            }

            return builder.AddAzureQueueStorageTransport(options =>
            {
                options.ConnectionString = connectionString;
                configure?.Invoke(options);
            });
        }
    }
}
