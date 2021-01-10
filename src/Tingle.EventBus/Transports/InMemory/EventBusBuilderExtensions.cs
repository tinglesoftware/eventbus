using Microsoft.Extensions.Options;
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
        public static EventBusBuilder AddInMemoryTransport(this EventBusBuilder builder, Action<InMemoryTransportOptions> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            // configure the options for Azure Service Bus
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.AddSingleton<SequenceNumberGenerator>();

            // register the transport
            builder.RegisterTransport<InMemoryTransport, InMemoryTransportOptions>();

            return builder;
        }

        /// <summary>
        /// Add InMemory test harness. This can be reolved using <see cref="InMemoryTestHarness"/>.
        /// <br/>
        /// Ensure the InMemory transport has been added using <see cref="AddInMemoryTestHarness(EventBusBuilder)"/>
        /// before resolving instances of <see cref="InMemoryTestHarness"/>.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static EventBusBuilder AddInMemoryTestHarness(this EventBusBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            // Register the harness
            services.AddSingleton<InMemoryTestHarness>();

            // Set the delivery delay to zero for instance delivery
            services.Configure<InMemoryTransportOptions>(o => o.DeliveryDelay = TimeSpan.Zero);
            services.AddSingleton<IPostConfigureOptions<InMemoryTestHarnessOptions>, InMemoryTestHarnessPostConfigureOptions>();

            return builder;
        }
    }
}
