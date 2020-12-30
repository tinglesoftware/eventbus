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
        public static EventBusBuilder AddAzureQueueStorage(this EventBusBuilder builder, Action<AzureQueueStorageOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Azure Queue Storage
            services.Configure(configure);
            services.PostConfigure<AzureQueueStorageOptions>(options =>
            {
                // ensure the connection string
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    throw new InvalidOperationException($"The '{nameof(options.ConnectionString)}' must be provided");
                }

                // ensure delay is within 30sec and 10min bounds
                if (options.EmptyResultsDelay < TimeSpan.FromSeconds(30) || options.EmptyResultsDelay > TimeSpan.FromMinutes(10))
                {
                    throw new InvalidOperationException($"The '{nameof(options.EmptyResultsDelay)}' must be between 30 seconds and 10 minutes.");
                }
            });

            // register the transport
            builder.RegisterTransport<AzureQueueStorageTransport>();

            return builder;
        }

        /// <summary>
        /// Add Azure Queue Storage as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="connectionString">
        /// The connection string to the Azure Storage account.
        /// Maps to <see cref="AzureQueueStorageOptions.ConnectionString"/>.
        /// </param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddAzureQueueStorage(this EventBusBuilder builder,
                                                           string connectionString,
                                                           Action<AzureQueueStorageOptions> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace", nameof(connectionString));
            }

            return builder.AddAzureQueueStorage(options =>
            {
                options.ConnectionString = connectionString;
                configure?.Invoke(options);
            });
        }
    }
}
