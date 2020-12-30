using System;
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
        public static EventBusBuilder AddInMemory(this EventBusBuilder builder, Action<InMemoryOptions> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            // configure the options for Azure Service Bus
            if (configure != null)
            {
                services.Configure(configure);
            }

            // register the transport
            builder.RegisterTransport<InMemoryTransport>();

            return builder;
        }
    }
}
