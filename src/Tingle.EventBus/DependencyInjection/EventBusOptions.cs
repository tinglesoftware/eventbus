using Microsoft.Extensions.Hosting;
using Polly;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Represents all the transport-agnostic options you can use to configure the Event Bus.
/// </summary>
public class EventBusOptions
{
    private readonly IList<EventBusTransportRegistrationBuilder> transports = new List<EventBusTransportRegistrationBuilder>();

    /// <summary>
    /// Gets the <see cref="EventBusNamingOptions"/> for the Event Bus.
    /// </summary>
    public EventBusNamingOptions Naming { get; } = new EventBusNamingOptions();

    /// <summary>
    /// Gets or sets the default setting whether transports should wait to be ready before publishing/cancelling events.
    /// Set this to false when not using or starting an <see cref="IHost"/> because the bus and its transports
    /// are started in an <see cref="IHostedService"/>.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// To specify a value on a transport, use <see cref="EventBusTransportOptions.WaitTransportStarted"/> for the specific transport.
    /// </remarks>
    public bool DefaultTransportWaitStarted { get; set; } = true;

    /// <summary>
    /// Optional resilience pipeline to apply to the bus.
    /// When provided alongside pipelines on the transport and the event registration, it is used as the putter most pipeline.
    /// Defaults to <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// To specify a value on an event registration, use <see cref="EventRegistration.ResiliencePipeline"/>.
    /// To specify a value on a transport, use <see cref="EventBusTransportOptions.ResiliencePipeline"/> for the specific transport.
    /// </remarks>
    public ResiliencePipeline? ResiliencePipeline { get; set; }

    /// <summary>
    /// Optional default format to use for generated event identifiers when for events where it is not specified.
    /// To specify a value per consumer, use the <see cref="EventRegistration.IdFormat"/> option.
    /// To specify a value per transport, use the <see cref="EventBusTransportOptions.DefaultEventIdFormat"/> option on the specific transport.
    /// Defaults to <see cref="EventIdFormat.Guid"/>.
    /// </summary>
    public EventIdFormat DefaultEventIdFormat { get; set; } = EventIdFormat.Guid;

    /// <summary>
    /// Gets or sets the default setting for the duration of duplicate detection history that is maintained by a transport.
    /// When not null, duplicate messages having the same <see cref="EventContext.Id"/> sent to the same destination within
    /// the given duration will be discarded.
    /// Defaults to <see langword="null"/>.
    /// To specify a value per consumer, use the <see cref="EventRegistration.DuplicateDetectionDuration"/> option.
    /// To specify a value per transport, use the <see cref="EventBusTransportOptions.DefaultDuplicateDetectionDuration"/> option on the specific transport.
    /// </summary>
    /// <remarks>
    /// Duplicate detection can only be done on the transport layer because it requires persistent storage.
    /// This feature only works if the transport an event is sent on supports duplicate detection.
    /// </remarks>
    public TimeSpan? DefaultDuplicateDetectionDuration { get; set; }

    /// <summary>
    /// Optional default behaviour for errors encountered in a consumer but are not handled.
    /// To specify a value per consumer, use the <see cref="EventConsumerRegistration.UnhandledErrorBehaviour"/> option.
    /// To specify a value per transport, use the <see cref="EventBusTransportOptions.DefaultUnhandledConsumerErrorBehaviour"/> option on the specific transport.
    /// When an <see cref="ResiliencePipeline"/> is in force, only errors that are not handled by it will be subject to the value set here.
    /// Defaults to <see langword="null"/>.
    /// </summary>
    public UnhandledConsumerErrorBehaviour? DefaultUnhandledConsumerErrorBehaviour { get; set; }

    /// <summary>
    /// Gets or sets the name of the default transport.
    /// When there is only one transport registered, setting this value is not necessary, as it is used as the default.
    /// </summary>
    public string? DefaultTransportName { get; set; }

    /// <summary>Transports in the order they were added.</summary>
    public IEnumerable<EventBusTransportRegistrationBuilder> Transports => transports;

    /// <summary>Maps transports by name.</summary>
    internal IDictionary<string, EventBusTransportRegistrationBuilder> TransportMap { get; } = new Dictionary<string, EventBusTransportRegistrationBuilder>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The registrations for events and consumers for the EventBus.
    /// </summary>
    internal ConcurrentDictionary<Type, EventRegistration> Registrations { get; } = new();


    /// <summary>Adds a <see cref="EventBusTransportRegistration"/>.</summary>
    /// <param name="name">The name of the transport being added.</param>
    /// <param name="configureBuilder">Configures the transport.</param>
    public void AddTransport(string name, Action<EventBusTransportRegistrationBuilder> configureBuilder)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (configureBuilder == null) throw new ArgumentNullException(nameof(configureBuilder));
        if (TransportMap.ContainsKey(name)) throw new InvalidOperationException("Transport already exists: " + name);

        var builder = new EventBusTransportRegistrationBuilder(name);
        configureBuilder(builder);
        transports.Add(builder);
        TransportMap[name] = builder;
    }

    /// <summary>Adds a <see cref="EventBusTransportRegistration"/>.</summary>
    /// <typeparam name="THandler">The <see cref="IEventBusTransport"/> responsible for the transport.</typeparam>
    /// <param name="name">The name of the transport being added.</param>
    /// <param name="displayName">The display name for the transport.</param>
    public void AddTransport<[DynamicallyAccessedMembers(TrimmingHelper.Transport)]THandler>(string name, string? displayName) where THandler : IEventBusTransport
        => AddTransport(name, b =>
        {
            b.DisplayName = displayName;
            b.TransportType = typeof(THandler);
        });

    /// <summary>
    /// Gets the consumer registrations for a given transport.
    /// </summary>
    /// <param name="transportName">The name of the transport for whom to get registered consumers.</param>
    /// <returns></returns>
    public ICollection<EventRegistration> GetRegistrations(string transportName)
    {
        if (string.IsNullOrWhiteSpace(transportName))
        {
            throw new ArgumentException($"'{nameof(transportName)}' cannot be null or whitespace", nameof(transportName));
        }

        // filter out the consumers where the event is set for the given transport
        return Registrations.Values.Where(r => r.TransportName == transportName).ToList();
    }

    /// <summary>
    /// Get the consumer registrations in a given event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type from which to retrieve a <see cref="EventConsumerRegistration"/> for.</typeparam>
    /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
    /// <param name="reg">
    /// When this method returns, contains the event registrations associated with the specified event type,
    /// if the event type is found; otherwise, <see langword="null"/> is returned.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <param name="ecrs">
    /// When this method returns, contains the consumer registrations associated with the specified event type,
    /// if the event type is found; otherwise, <see langword="null"/> is returned.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
    internal bool TryGetConsumerRegistrations<TEvent, TConsumer>([NotNullWhen(true)] out EventRegistration? reg,
                                                                 [NotNullWhen(true)] out List<EventConsumerRegistration>? ecrs)
    {
        ecrs = default;
        if (Registrations.TryGetValue(typeof(TEvent), out reg))
        {
            ecrs = reg.Consumers.Where(r => r.ConsumerType == typeof(TConsumer)).ToList();
            return false;
        }
        return false;
    }

    /// <summary>
    /// Get the consumer registrations in a given event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type from which to retrieve a <see cref="EventConsumerRegistration"/> for.</typeparam>
    /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
    /// <param name="registrations">
    /// When this method returns, contains the consumer registrations associated with the specified event type,
    /// if the event type is found; otherwise, <see langword="null"/> is returned.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
    public bool TryGetConsumerRegistrations<TEvent, TConsumer>([NotNullWhen(true)] out List<EventConsumerRegistration>? registrations)
    {
        return TryGetConsumerRegistrations<TEvent, TConsumer>(out _, out registrations);
    }

    /// <summary>
    /// Configure the <see cref="EventRegistration"/> for <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event to configure for</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusOptions ConfigureEvent<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(Action<EventRegistration> configure) where TEvent : class
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        // if there's already a registration for the event return it
        var eventType = typeof(TEvent);
        var registration = Registrations.GetOrAdd(eventType, et => EventRegistration.Create<TEvent>());
        configure(registration);

        return this;
    }

    /// <summary>
    /// Configure the <see cref="EventConsumerRegistration"/> for <typeparamref name="TConsumer"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event in the consumer to configure for.</typeparam>
    /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusOptions ConfigureConsumer<TEvent, TConsumer>(Action<EventRegistration, EventConsumerRegistration> configure)
        where TConsumer : class, IEventConsumer
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        if (TryGetConsumerRegistrations<TEvent, TConsumer>(out var reg, out var ecrs) && ecrs is not null)
        {
            foreach (var ecr in ecrs) configure(reg, ecr);
        }

        return this;
    }

    /// <summary>
    /// Configure the <see cref="EventConsumerRegistration"/> for <typeparamref name="TConsumer"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event in the consumer to configure for.</typeparam>
    /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusOptions ConfigureConsumer<TEvent, TConsumer>(Action<EventConsumerRegistration> configure)
        where TConsumer : class, IEventConsumer
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        return ConfigureConsumer<TEvent, TConsumer>((reg, ecr) => configure(ecr));
    }
}
