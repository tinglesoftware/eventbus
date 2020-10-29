using System;
using Tingle.EventBus.Abstractions;
using Tingle.EventBus.Transports.InMemory;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for InMemory.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add InMemory as the underlying transport for the Event Bus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddInMemory(this EventBusBuilder builder, Action<InMemoryOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var services = builder.Services;

            // configure the options for Azure Service Bus
            services.Configure(configure);

            // register the event bus
            services.AddSingleton<IEventBus, InMemoryEventBus>();

            return builder;
        }
    }
}
