using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus;
using Tingle.EventBus.Registrations;
using Tingle.EventBus.Serialization;

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

            Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IPostConfigureOptions<EventBusOptions>, EventBusPostConfigureOptions>());
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
            Services.Configure(configure);
            return this;
        }

        /// <summary>
        /// Setup the serializer to use when serializing events to and from the EventBus transport.
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
        /// <returns></returns>
        public EventBusBuilder Subscribe<TConsumer>() where TConsumer : class, IEventBusConsumer
        {
            /*
             * Register the consumer to resolution. Transient scope is better here because
             * we would rather create an instance for each time we need rather than have
             * leakage of depenencies.
            */
            Services.AddTransient<TConsumer>();

            var genericConsumerType = typeof(IEventBusConsumer<>);
            var eventTypes = new List<Type>();

            var consumerType = typeof(TConsumer);
            if (consumerType.IsAbstract)
            {
                throw new InvalidOperationException($"Abstract consumer types are not allowed.");
            }

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
                throw new InvalidOperationException($"{consumerType.FullName} must implement '{nameof(IEventBusConsumer)}<TEvent>' at least once.");
            }

            // add the event types to the registrations
            return Configure(options =>
            {
                foreach (var et in eventTypes)
                {
                    // if the type is already mapped to another consumer, throw meaningful exception
                    if (options.ConsumerRegistrations.TryGetValue(et, out var ct)
                        && ct.ConsumerType != consumerType)
                    {
                        throw new InvalidOperationException($"{et.FullName} cannot be mapped to {consumerType.FullName} as it is already mapped to {ct.ConsumerType.FullName}");
                    }

                    options.ConsumerRegistrations[et] = new ConsumerRegistration(eventType: et, consumerType: consumerType);
                }
            });
        }

        /// <summary>
        /// Unsubscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <returns></returns>
        public EventBusBuilder Unsubscribe<TConsumer>() where TConsumer : class, IEventBusConsumer
        {
            // Deregister from services collection
            Services.RemoveAll<TConsumer>();

            return Configure(options =>
            {
                var types = options.ConsumerRegistrations.Where(kvp => kvp.Value.ConsumerType == typeof(TConsumer))
                                                      .Select(kvp => kvp.Key)
                                                      .ToList();
                foreach (var r in types)
                {
                    options.ConsumerRegistrations.Remove(r);
                }
            });
        }

        /// <summary>
        /// Configure the <see cref="ConsumerRegistration"/> for <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The event to configure for</typeparam>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder ConfigureConsumer<TEvent>(Action<ConsumerRegistration> configure)
        {
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            return Configure(options =>
            {
                if (options.TryGetConsumerRegistration<TEvent>(out var registration))
                {
                    configure(registration);
                }
            });
        }

        /// <summary>
        /// Configure the <see cref="EventRegistration"/> for <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The event to configure for</typeparam>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder ConfigureEvent<TEvent>(Action<EventRegistration> configure)
        {
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            return Configure(options =>
            {
                var registration = options.GetOrCreateEventRegistration<TEvent>();
                configure(registration);
            });
        }
    }
}
