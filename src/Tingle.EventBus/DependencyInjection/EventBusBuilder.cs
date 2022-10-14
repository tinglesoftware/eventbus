using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Ids;
using Tingle.EventBus.Serialization;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A builder class for adding and configuring the EventBus in <see cref="IServiceCollection"/>.
/// </summary>
public class EventBusBuilder
{
    /// <summary>
    /// Creates an instance of <see cref="EventBusBuilder"/>
    /// </summary>
    /// <param name="services"></param>
    public EventBusBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));

        // Configure the options
        Services.ConfigureOptions<EventBusConfigureOptions>();
        Services.AddSingleton<IEventConfigurator, DefaultEventConfigurator>();
        Services.AddSingleton<IEventIdGenerator, DefaultEventIdGenerator>();

        // Register the bus and its host
        Services.AddSingleton<EventBus>();
        Services.AddHostedService<EventBusHost>();

        // Register necessary services
        Services.AddTransient<IEventPublisher, EventPublisher>();
        UseDefaultSerializer<DefaultJsonEventSerializer>();
    }

    /// <summary>
    /// The instance of <see cref="IServiceCollection"/> that this builder instance adds to.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Configure options for the EventBus.</summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder Configure(Action<EventBusOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        Services.Configure(configure);
        return this;
    }

    /// <summary>Configure serialization options for the EventBus.</summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder ConfigureSerialization(Action<EventBusSerializationOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        Services.Configure(configure);
        return this;
    }

    /// <summary>Adds a <see cref="EventBusTransportRegistration"/> which can be used by the event bus.</summary>
    /// <typeparam name="TOptions">The <see cref="EventBusTransportOptionsBase"/> type to configure the transport."/>.</typeparam>
    /// <typeparam name="THandler">The <see cref="EventBusTransportBase{TOptions}"/> used to handle this transport.</typeparam>
    /// <param name="name">The name of this transport.</param>
    /// <param name="configureOptions">Used to configure the transport options.</param>
    public virtual EventBusBuilder AddTransport<TOptions, THandler>(string name, Action<TOptions>? configureOptions)
        where TOptions : EventBusTransportOptionsBase, new()
        where THandler : EventBusTransportBase<TOptions>
        => AddTransport<TOptions, THandler>(name, displayName: null, configureOptions: configureOptions);

    /// <summary>Adds a <see cref="EventBusTransportRegistration"/> which can be used by the event bus.</summary>
    /// <typeparam name="TOptions">The <see cref="EventBusTransportOptionsBase"/> type to configure the transport."/>.</typeparam>
    /// <typeparam name="THandler">The <see cref="EventBusTransportBase{TOptions}"/> used to handle this transport.</typeparam>
    /// <param name="name">The name of this transport.</param>
    /// <param name="displayName">The display name of this transport.</param>
    /// <param name="configureOptions">Used to configure the transport options.</param>
    public virtual EventBusBuilder AddTransport<TOptions, THandler>(string name, string? displayName, Action<TOptions>? configureOptions)
        where TOptions : EventBusTransportOptionsBase, new()
        where THandler : EventBusTransportBase<TOptions>
        => AddTransportHelper<TOptions, THandler>(name, displayName, configureOptions);

    private EventBusBuilder AddTransportHelper<TOptions, TTransport>(string name, string? displayName, Action<TOptions>? configureOptions)
        where TOptions : EventBusTransportOptionsBase, new()
        where TTransport : class, IEventBusTransport
    {
        Services.Configure<EventBusOptions>(o => o.AddTransport<TTransport>(name, displayName));
        if (configureOptions is not null) Services.Configure(name, configureOptions);

        Services.ConfigureOptions<TransportOptionsConfigureOptions<TOptions>>();
        Services.AddSingleton<TTransport>();
        return this;
    }

    /// <summary>
    /// Unregister a transport already registered on the bus.
    /// </summary>
    /// <typeparam name="TTransport"></typeparam>
    /// <returns></returns>
    public EventBusBuilder RemoveTransport<TTransport>(string name) where TTransport : class, IEventBusTransport
    {
        // remove the service descriptor if it exists
        var target = Services.SingleOrDefault(t => t.ServiceType == typeof(IEventBusTransport) && t.ImplementationType == typeof(TTransport));
        if (target != null) Services.Remove(target);

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
}
