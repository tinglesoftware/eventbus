﻿using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus;
using Tingle.EventBus.Registrations;
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

            // Register necessary services
            Services.AddTransient<IEventPublisher, EventPublisher>();
            Services.AddSingleton<EventBus>();
            Services.AddHostedService(p => p.GetRequiredService<EventBus>());
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
            var tname = GetTransportName<TTransport>();

            // Add name to registered transports
            return Configure(options => options.RegisteredTransportNames.Add(tname, typeof(TTransport)));
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
            var tname = GetTransportName<TTransport>();

            // Remove name from registered transports
            return Configure(options => options.RegisteredTransportNames.Remove(tname));
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
        /// Use the included default serializer.
        /// </summary>
        /// <returns></returns>
        public EventBusBuilder UseDefaultSerializer() => UseDefaultSerializer<DefaultEventSerializer>();

        /// <summary>
        /// Subscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
        /// <param name="lifetime">
        /// The lifetime to use when resolving and maintaining instances of <typeparamref name="TConsumer"/>.
        /// Using <see cref="ServiceLifetime.Transient"/> is best because a clean instance is created each
        /// time a message is received and has a high level of isolation hence avoiding leackage of dependencies.
        /// However, it can result in high memory usage making <see cref="ServiceLifetime.Scoped"/> the
        /// middleground. Scoped instances are resued for each message received in a batch of messages so long
        /// as they are processed sequencially.
        /// <br />
        /// <br />
        /// These decisions do not apply in all scenarios and should be reconsidered depending on the
        /// design of <typeparamref name="TConsumer"/>.
        /// </param>
        /// <returns></returns>
        public EventBusBuilder AddConsumer<TConsumer>(ServiceLifetime lifetime = ServiceLifetime.Scoped) where TConsumer : class, IEventConsumer
        {
            var consumerType = typeof(TConsumer);
            if (consumerType.IsAbstract)
            {
                throw new InvalidOperationException($"Abstract consumer types are not allowed.");
            }

            // register the consumer for resolution
            Services.Add(ServiceDescriptor.Describe(consumerType, consumerType, lifetime));

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
                    // get or create a simple registration
                    if (!options.Registrations.TryGetValue(et, out var registration))
                    {
                        registration = options.Registrations[et] = new EventRegistration(et);
                    }

                    // add the consumer to the registration
                    registration.Consumers.Add(new EventConsumerRegistration(consumerType: consumerType));
                }
            });
        }

        /// <summary>
        /// Unsubscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <returns></returns>
        public EventBusBuilder RemoveConsumer<TConsumer>() where TConsumer : class, IEventConsumer
        {
            // Deregister from services collection
            Services.RemoveAll<TConsumer>();

            // Remove the event types
            return Configure(options =>
            {
                var ct = typeof(TConsumer);
                foreach (var registration in options.Registrations.Values)
                {
                    var target = registration.Consumers.SingleOrDefault(c => c.ConsumerType == ct);
                    if (target != null)
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
