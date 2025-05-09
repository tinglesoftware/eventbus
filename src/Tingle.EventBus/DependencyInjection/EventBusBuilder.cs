﻿using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Ids;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Serialization;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A builder class for adding and configuring the EventBus in <see cref="IServiceCollection"/>.
/// </summary>
public class EventBusBuilder
{
    private const string ConsumersRequiresDynamicCode = "Registering consumers for all subscribed events requires dynamic code that might not be available at runtime. Register the consumer once for every event using AddConsumer<TEvent, TConsumer>(...)";

    /// <summary>
    /// Creates an instance of <see cref="EventBusBuilder"/>
    /// </summary>
    /// <param name="services"></param>
    public EventBusBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));

        // Configure the options
        Services.ConfigureOptions<EventBusConfigureOptions>();

        // Register the bus and its host
        Services.TryAddSingleton<EventBus>();
        Services.AddHostedService<EventBusHost>();

        // Register necessary services
        Services.TryAddSingleton<EventBusTransportProvider>();
        Services.TryAddSingleton<IEventBusConfigurationProvider, DefaultEventBusConfigurationProvider>();
        Services.TryAddSingleton<IEventIdGenerator, DefaultEventIdGenerator>();
        Services.TryAddTransient<IEventPublisher, EventPublisher>();
        Services.AddSingleton<IEventBusConfigurator, MandatoryEventBusConfigurator>(); // can be multiple do not use TryAdd*(...)
        AddSerializer<DefaultJsonEventSerializer>();
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
    /// <typeparam name="THandler">The <see cref="EventBusTransport{TOptions}"/> used to handle this transport.</typeparam>
    /// <typeparam name="TOptions">The <see cref="EventBusTransportOptions"/> type to configure the transport."/>.</typeparam>
    /// <typeparam name="TConfigurator">The <see cref="EventBusTransportConfigureOptions{TOptions}"/> type to configure the <typeparamref name="TOptions"/>."/>.</typeparam>
    /// <param name="name">The name of this transport.</param>
    /// <param name="configureOptions">Used to configure the transport options.</param>
    public EventBusBuilder AddTransport<[DynamicallyAccessedMembers(TrimmingHelper.Transport)] THandler, TOptions, [DynamicallyAccessedMembers(TrimmingHelper.Configurator)] TConfigurator>(
        string name, Action<TOptions>? configureOptions)
        where THandler : EventBusTransport<TOptions>
        where TOptions : EventBusTransportOptions, new()
        where TConfigurator : EventBusTransportConfigureOptions<TOptions>
        => AddTransport<THandler, TOptions, TConfigurator>(name, displayName: null, configureOptions: configureOptions);

    /// <summary>Adds a <see cref="EventBusTransportRegistration"/> which can be used by the event bus.</summary>
    /// <typeparam name="THandler">The <see cref="EventBusTransport{TOptions}"/> used to handle this transport.</typeparam>
    /// <typeparam name="TOptions">The <see cref="EventBusTransportOptions"/> type to configure the transport."/>.</typeparam>
    /// <typeparam name="TConfigurator">The <see cref="EventBusTransportConfigureOptions{TOptions}"/> type to configure the <typeparamref name="TOptions"/>."/>.</typeparam>
    /// <param name="name">The name of this transport.</param>
    /// <param name="displayName">The display name of this transport.</param>
    /// <param name="configureOptions">Used to configure the transport options.</param>
    public EventBusBuilder AddTransport<[DynamicallyAccessedMembers(TrimmingHelper.Transport)] THandler, TOptions, [DynamicallyAccessedMembers(TrimmingHelper.Configurator)] TConfigurator>(
        string name, string? displayName, Action<TOptions>? configureOptions)
        where THandler : EventBusTransport<TOptions>
        where TOptions : EventBusTransportOptions, new()
        where TConfigurator : EventBusTransportConfigureOptions<TOptions>
        => AddTransportHelper<THandler, TOptions, TConfigurator>(name, displayName, configureOptions);

    private EventBusBuilder AddTransportHelper<[DynamicallyAccessedMembers(TrimmingHelper.Transport)] TTransport, TOptions, [DynamicallyAccessedMembers(TrimmingHelper.Configurator)] TConfigurator>(
        string name, string? displayName, Action<TOptions>? configureOptions)
        where TTransport : class, IEventBusTransport
        where TOptions : EventBusTransportOptions, new()
        where TConfigurator : EventBusTransportConfigureOptions<TOptions>
    {
        Services.Configure<EventBusOptions>(o => o.AddTransport<TTransport>(name, displayName));

        // Calling ConfigureOptions results in more than one call to the configuration implementations so we have to implement our own logic
        // TODO: remove after https://github.com/dotnet/runtime/issues/42358 is addressed

        static IEnumerable<Type> FindConfigurationServices([DynamicallyAccessedMembers(TrimmingHelper.Configurator)] Type type)
        {
            var interfaces = type.GetInterfaces();
            foreach (var t in interfaces)
            {
                if (t.IsGenericType)
                {
                    var gtd = t.GetGenericTypeDefinition();
                    if (gtd == typeof(Options.IConfigureOptions<>) || gtd == typeof(Options.IPostConfigureOptions<>) || gtd == typeof(Options.IValidateOptions<>))
                    {
                        yield return t;
                    }
                }
            }
        }
        foreach (var serviceType in FindConfigurationServices(typeof(TConfigurator)))
        {
            Services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, typeof(TConfigurator)));
        }

        if (configureOptions is not null) Services.Configure(name, configureOptions);

        // //  transport is not registered because multiple separate instances are required per name
        // Services.TryAddSingleton<TTransport>();
        return this;
    }

    /// <summary> Add serializer to use when serializing events to and from a transport.</summary>
    /// <typeparam name="TEventSerializer"></typeparam>
    /// <returns></returns>
    public EventBusBuilder AddSerializer<[DynamicallyAccessedMembers(TrimmingHelper.Serializer)] TEventSerializer>()
        where TEventSerializer : class, IEventSerializer
    {
        Services.AddSingleton<IEventSerializer, TEventSerializer>(); // can be multiple do not use TryAdd*(...)
        return this;
    }

    /// <summary> Add serializer to use when serializing events to and from a transport.</summary>
    /// <typeparam name="TEventSerializer"></typeparam>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns></returns>
    public EventBusBuilder AddSerializer<TEventSerializer>(Func<IServiceProvider, TEventSerializer> implementationFactory)
        where TEventSerializer : class, IEventSerializer
    {
        Services.AddSingleton<IEventSerializer>(implementationFactory);
        return this;
    }

    /// <summary>Subscribe a consumer to events of a single type.</summary>
    /// <typeparam name="TEvent">The type of event to listen to.</typeparam>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder AddConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>(
        Action<EventRegistration, EventConsumerRegistration> configure)
        where TEvent : class
        where TConsumer : class, IEventConsumer<TEvent>
    {
        var eventType = typeof(TEvent);
        if (eventType.IsAbstract) throw new InvalidOperationException($"Abstract event types are not allowed.");

        var consumerType = typeof(TConsumer);
        if (consumerType.IsAbstract) throw new InvalidOperationException($"Abstract consumer types are not allowed.");

        // add the event types to the registrations
        return Configure(options =>
        {
            // get or create a simple EventRegistration
            var reg = options.Registrations.GetOrAdd(eventType, t => EventRegistration.Create<TEvent>());

            // create a simple ConsumerRegistration (HashSet removes duplicates)
            var ecr = EventConsumerRegistration.Create<TEvent, TConsumer>();
            reg.Consumers.Add(ecr);

            // call the configuration function
            configure?.Invoke(reg, ecr);
        });
    }

    /// <summary>Subscribe a consumer to deadletter events of a single type.</summary>
    /// <typeparam name="TEvent">The type of event to listen to.</typeparam>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder AddDeadLetteredConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>(
        Action<EventRegistration, EventConsumerRegistration> configure)
        where TEvent : class
        where TConsumer : class, IDeadLetteredEventConsumer<TEvent>
    {
        var eventType = typeof(TEvent);
        if (eventType.IsAbstract) throw new InvalidOperationException($"Abstract event types are not allowed.");

        var consumerType = typeof(TConsumer);
        if (consumerType.IsAbstract) throw new InvalidOperationException($"Abstract consumer types are not allowed.");

        // add the event types to the registrations
        return Configure(options =>
        {
            // get or create a simple EventRegistration
            var reg = options.Registrations.GetOrAdd(eventType, t => EventRegistration.Create<TEvent>());

            // create a simple ConsumerRegistration (HashSet removes duplicates)
            var ecr = EventConsumerRegistration.CreateDeadLettered<TEvent, TConsumer>();
            reg.Consumers.Add(ecr);

            // call the configuration function
            configure?.Invoke(reg, ecr);
        });
    }

    /// <summary>Subscribe to events that a consumer can listen to.</summary>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    [RequiresDynamicCode(ConsumersRequiresDynamicCode)]
    [RequiresUnreferencedCode(MessageStrings.GenericsUnreferencedCodeMessage)]
    public EventBusBuilder AddConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>(
        Action<EventRegistration, EventConsumerRegistration> configure) where TConsumer : class, IEventConsumer
    {
        var consumerType = typeof(TConsumer);
        if (consumerType.IsAbstract)
        {
            throw new InvalidOperationException($"Abstract consumer types are not allowed.");
        }

        var eventTypes = new List<(Type type, bool deadletter)>();

        // get events from each implementation of IEventConsumer<TEvent> or IDeadLetteredEventConsumer<TEvent>
        var interfaces = consumerType.GetInterfaces();
        foreach (var type in interfaces)
        {
            if (!type.IsGenericType) continue;

            var gtd = type.GetGenericTypeDefinition();
            if (gtd != typeof(IEventConsumer<>) && gtd != typeof(IDeadLetteredEventConsumer<>)) continue;

            eventTypes.Add((type.GenericTypeArguments[0], gtd == typeof(IDeadLetteredEventConsumer<>)));
        }

        // we must have at least one implemented event
        if (eventTypes.Count <= 0)
        {
            throw new InvalidOperationException($"{consumerType.FullName} must implement '{nameof(IEventConsumer)}<TEvent>' at least once.");
        }

        foreach (var (et, _) in eventTypes)
        {
            if (et.IsAbstract)
            {
                throw new InvalidOperationException($"Invalid event type '{et.FullName}'. Abstract types are not allowed.");
            }
        }

        // add the event types to the registrations
        return Configure(options =>
        {
            foreach (var (et, deadletter) in eventTypes)
            {
                // get or create a simple EventRegistration
                var reg = options.Registrations.GetOrAdd(et, t => EventRegistration.Create(et));

                // create a simple ConsumerRegistration (HashSet removes duplicates)
                var ecr = EventConsumerRegistration.Create(et, consumerType, deadletter);
                reg.Consumers.Add(ecr);

                // call the configuration function
                configure?.Invoke(reg, ecr);
            }
        });
    }

    /// <summary>Subscribe to events that a consumer can listen to.</summary>
    /// <typeparam name="TEvent">The type of event to listen to.</typeparam>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder AddConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>(
        Action<EventConsumerRegistration>? configure = null)
        where TEvent : class
        where TConsumer : class, IEventConsumer<TEvent>
    {
        return AddConsumer<TEvent, TConsumer>((reg, ecr) => configure?.Invoke(ecr));
    }

    /// <summary>Subscribe to deadletter events that a consumer can listen to.</summary>
    /// <typeparam name="TEvent">The type of event to listen to.</typeparam>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder AddDeadLetteredConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>(
        Action<EventConsumerRegistration>? configure = null)
        where TEvent : class
        where TConsumer : class, IDeadLetteredEventConsumer<TEvent>
    {
        return AddDeadLetteredConsumer<TEvent, TConsumer>((reg, ecr) => configure?.Invoke(ecr));
    }

    /// <summary>Subscribe to events that a consumer can listen to.</summary>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    [RequiresDynamicCode(ConsumersRequiresDynamicCode)]
    [RequiresUnreferencedCode(MessageStrings.GenericsUnreferencedCodeMessage)]
    public EventBusBuilder AddConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>(
        Action<EventConsumerRegistration>? configure = null) where TConsumer : class, IEventConsumer
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
                registration.Consumers.RemoveWhere(creg => creg.ConsumerType == ct);
            }
        });
    }
}
