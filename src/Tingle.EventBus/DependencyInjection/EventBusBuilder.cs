using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Ids;
using Tingle.EventBus.Readiness;
using Tingle.EventBus.Serialization;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A builder class for adding and configuring the EventBus in <see cref="IServiceCollection"/>.
    /// </summary>
    public class EventBusBuilder
    {
        /// <summary>
        /// Creates an instance os <see cref="EventBusBuilder"/>
        /// </summary>
        /// <param name="services"></param>
        public EventBusBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));

            // Configure the options
            Services.AddSingleton<IConfigureOptions<EventBusOptions>, EventBusConfigureOptions>();
            Services.AddSingleton<IPostConfigureOptions<EventBusOptions>, EventBusPostConfigureOptions>();
            Services.AddSingleton<IEventConfigurator, DefaultEventConfigurator>();
            Services.AddSingleton<IEventIdGenerator, DefaultEventIdGenerator>();

            // Register necessary services
            Services.AddTransient<IEventPublisher, EventPublisher>();
            Services.AddSingleton<EventBus>();
            Services.AddHostedService(p => p.GetRequiredService<EventBus>());
            UseDefaultSerializer<DefaultJsonEventSerializer>();

            // Register health/readiness services needed
            Services.AddSingleton<DefaultReadinessProvider>();
            Services.AddSingleton<IHealthCheckPublisher>(p => p.GetRequiredService<DefaultReadinessProvider>());
            Services.AddSingleton<IReadinessProvider>(p => p.GetRequiredService<DefaultReadinessProvider>());
        }

        /// <summary>
        /// The instance of <see cref="IServiceCollection"/> that this builder instance adds to.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Configure options for EventBus
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder Configure(Action<EventBusOptions> configure)
        {
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            Services.Configure(configure);
            return this;
        }

        /// <summary>
        /// Register a transport to be used by the bus.
        /// </summary>
        /// <typeparam name="TTransport"></typeparam>
        /// <typeparam name="TOptions"></typeparam>
        /// <returns></returns>
        public EventBusBuilder AddTransport<TTransport, TOptions>()
            where TTransport : class, IEventBusTransport
            where TOptions : EventBusTransportOptionsBase
        {
            // Post configure the common transport options
            Services.AddSingleton<IPostConfigureOptions<TOptions>, TransportOptionsPostConfigureOptions<TOptions>>();

            // Register for resolution
            Services.AddSingleton<IEventBusTransport, TTransport>();

            // Get the name of the transport
            var name = GetTransportName<TTransport>();

            // Add name to registered transports
            return Configure(options => options.RegisteredTransportNames.Add(name, typeof(TTransport)));
        }

        /// <summary>
        /// Unregister a transport already registered on the bus.
        /// </summary>
        /// <typeparam name="TTransport"></typeparam>
        /// <returns></returns>
        public EventBusBuilder RemoveTransport<TTransport>() where TTransport : class, IEventBusTransport
        {
            // remove the service descriptor if it exists
            var target = Services.SingleOrDefault(t => t.ServiceType == typeof(IEventBusTransport) && t.ImplementationType == typeof(TTransport));
            if (target != null) Services.Remove(target);

            // Get the name of the transport
            var name = GetTransportName<TTransport>();

            // Remove name from registered transports
            return Configure(options => options.RegisteredTransportNames.Remove(name));
        }

        /// <summary>
        /// Setup the default serializer to use when serializing events to and from the EventBus transport.
        /// </summary>
        /// <typeparam name="TEventSerializer"></typeparam>
        /// <returns></returns>
        public EventBusBuilder UseDefaultSerializer<TEventSerializer>() where TEventSerializer : class, IEventSerializer
        {
            Services.AddSingleton<IEventSerializer, TEventSerializer>();
            return this;
        }

        /// <summary>
        /// Setup the default readiness provider to use when deciding if a consumer is ready to consume events.
        /// </summary>
        /// <typeparam name="TReadinessProvider"></typeparam>
        /// <returns></returns>
        public EventBusBuilder UseDefaultReadinessProvider<TReadinessProvider>() where TReadinessProvider : class, IReadinessProvider
        {
            Services.AddSingleton<IReadinessProvider, TReadinessProvider>();
            return this;
        }

        /// <summary>
        /// Subscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder AddConsumer<TConsumer>(Action<EventRegistration, EventConsumerRegistration> configure) where TConsumer : class, IEventConsumer
        {
            var consumerType = typeof(TConsumer);
            if (consumerType.IsAbstract)
            {
                throw new InvalidOperationException($"Abstract consumer types are not allowed.");
            }

            var genericConsumerType = typeof(IEventConsumer<>);
            var eventTypes = new List<Type>();

            // get events from each implementation of IEventConsumer<TEvent>
            var interfaces = consumerType.GetInterfaces();
            foreach (var ifType in interfaces)
            {
                if (ifType.IsGenericType && ifType.GetGenericTypeDefinition() == genericConsumerType)
                {
                    var et = ifType.GenericTypeArguments[0];
                    if (et.IsAbstract)
                    {
                        throw new InvalidOperationException($"Invalid event type '{et.FullName}'. Abstract types are not allowed.");
                    }

                    eventTypes.Add(et);
                }
            }

            // we must have at least one implemented event
            if (eventTypes.Count <= 0)
            {
                throw new InvalidOperationException($"{consumerType.FullName} must implement '{nameof(IEventConsumer)}<TEvent>' at least once.");
            }

            // add the event types to the registrations
            return Configure(options =>
            {
                foreach (var et in eventTypes)
                {
                    // get or create a simple EventRegistration
                    if (!options.Registrations.TryGetValue(et, out var reg))
                    {
                        reg = options.Registrations[et] = new EventRegistration(et);
                    }

                    // create a ConsumerRegistration
                    var ecr = new EventConsumerRegistration(consumerType: consumerType);

                    // call the configuration function
                    configure?.Invoke(reg, ecr);

                    // add the consumer to the registration
                    reg.Consumers.Add(ecr);
                }
            });
        }

        /// <summary>
        /// Subscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder AddConsumer<TConsumer>(Action<EventConsumerRegistration>? configure = null) where TConsumer : class, IEventConsumer
        {
            return AddConsumer<TConsumer>((reg, ecr) => configure?.Invoke(ecr));
        }

        /// <summary>
        /// Unsubscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <returns></returns>
        public EventBusBuilder RemoveConsumer<TConsumer>() where TConsumer : class, IEventConsumer
        {
            // Remove the event types
            return Configure(options =>
            {
                var ct = typeof(TConsumer);
                foreach (var registration in options.Registrations.Values)
                {
                    var target = registration.Consumers.SingleOrDefault(c => c.ConsumerType == ct);
                    if (target is not null)
                    {
                        registration.Consumers.Remove(target);
                    }
                }
            });
        }

        private static string GetTransportName<TTransport>() where TTransport : IEventBusTransport => GetTransportName(typeof(TTransport));

        internal static string GetTransportName(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            // Ensure the type implements IEventBusTransport
            if (!(typeof(IEventBusTransport).IsAssignableFrom(type)))
            {
                throw new InvalidOperationException($"'{type.FullName}' must implement '{typeof(IEventBusTransport).FullName}'.");
            }

            // Ensure the TransportNameAttribute attribute is declared on it
            var attrs = type.GetCustomAttributes(false).OfType<TransportNameAttribute>().ToList();
            if (attrs.Count == 0)
            {
                throw new InvalidOperationException($"'{type.FullName}' must have '{typeof(TransportNameAttribute).FullName}' declared on it.");
            }

            // Ensure there is only one attribute and get the name
            return attrs.Single().Name;
        }
    }
}
