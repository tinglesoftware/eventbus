using System;
using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.EventHubs;

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
        public static EventBusBuilder AddAzureEventHubs(this EventBusBuilder builder, Action<AzureEventHubsOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Azure Service Bus
            services.Configure(configure);
            services.PostConfigure<AzureEventHubsOptions>(options =>
            {

            });

            // register the event bus
            services.AddSingleton<IEventBus, AzureEventHubsEventBus>();

            return builder;
        }
    }
}
