using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus.Abstractions;
using Tingle.EventBus.Abstractions.Serialization;

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

            // Register resolution for HostInfo
            Services.AddSingleton(p =>
            {
                var env = p.GetRequiredService<IHostEnvironment>();
                var entry = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetCallingAssembly();
                return new HostInfo
                {
                    ApplicationName = env.ApplicationName,
                    ApplicationVersion = entry.GetName().Version.ToString(),
                    EnvironmentName = env.EnvironmentName,
                    LibraryVersion = typeof(IEventBus).Assembly.GetName().Version.ToString(),
                    MachineName = Environment.MachineName,
                    OperatingSystem = Environment.OSVersion.ToString(),
                };
            });
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
        public EventBusBuilder UseSerializer<TEventSerializer>() where TEventSerializer : class, IEventSerializer
        {
            Services.AddSingleton<IEventSerializer, TEventSerializer>();
            return this;
        }

        /// <summary>
        /// Use serializer powered by <see href="https://www.nuget.org/packages/Newtonsoft.Json/">Newtonsoft.Json</see>.
        /// </summary>
        /// <returns></returns>
        public EventBusBuilder UseNewtonsoftJsonSerializer() => UseSerializer<NewtonsoftJsonEventSerializer>();

        /// <summary>
        /// Subscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
        /// <returns></returns>
        public EventBusBuilder Subscribe<TConsumer>() where TConsumer : class, IEventBusConsumer
        {
            // register the consumer to resolution
            // transient types are better here because we would rather create an instance for each tyime we need than have leakage of depenencies.
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

                    // TODO: check/ensure there is an empty constructor

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
                    if (options.EventRegistrations.TryGetValue(et, out var ct)
                        && ct != consumerType)
                    {
                        throw new InvalidOperationException($"{et.FullName} cannot be mapped to {consumerType.FullName} as it is already mapped to {ct.FullName}");
                    }

                    options.EventRegistrations[et] = consumerType;
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
            return Configure(options =>
            {
                var types = options.EventRegistrations.Where(kvp => kvp.Value == typeof(TConsumer))
                                                      .Select(kvp => kvp.Value)
                                                      .ToList();
                foreach (var r in types)
                {
                    options.EventRegistrations.Remove(r);
                }
            });
        }
    }
}
