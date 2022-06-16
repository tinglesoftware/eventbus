using Polly.Retry;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Serialization;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Represents all the transport-agnostic options you can use to configure the Event Bus.
/// </summary>
public class EventBusOptions
{
    /// <summary>
    /// The duration of time to delay the starting of the bus.
    /// Max value is 10 minutes and minimum is 5 seconds.
    /// When <see langword="null"/>, the bus is started immediately.
    /// </summary>
    public TimeSpan? StartupDelay { get; set; }

    /// <summary>
    /// Gets the <see cref="EventBusReadinessOptions"/> for the Event Bus.
    /// </summary>
    public EventBusReadinessOptions Readiness { get; } = new EventBusReadinessOptions();

    /// <summary>
    /// Gets the <see cref="EventBusNamingOptions"/> for the Event Bus.
    /// </summary>
    public EventBusNamingOptions Naming { get; } = new EventBusNamingOptions();

    /// <summary>
    /// The options to use for serialization.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                       | System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        WriteIndented = false, // less data used
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
                               | System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// The information about the host where the EventBus is running.
    /// </summary>
    public HostInfo? HostInfo { get; set; }

    /// <summary>
    /// Indicates if the messages/events produced require guard against duplicate messages.
    /// If <see langword="true"/>, duplicate messages having the same <see cref="EventContext.Id"/>
    /// sent to the same destination within a duration of <see cref="DuplicateDetectionDuration"/> will be discarded.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Duplicate detection can only be done on the transport layer because it requires peristent storage.
    /// This feature only works if the transport a message is sent on supports duplicate detection.
    /// </remarks>
    public bool EnableDeduplication { get; set; } = false;

    /// <summary>
    /// The <see cref="TimeSpan"/> duration of duplicate detection history that is maintained by a transport.
    /// </summary>
    /// <remarks>
    /// The default value is 1 minute. Max value is 7 days and minimum is 20 seconds.
    /// This value is only relevant if <see cref="EnableDeduplication"/> is set to <see langword="true"/>.
    /// </remarks>
    public TimeSpan DuplicateDetectionDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Optional default format to use for generated event identifiers when for events where it is not specified.
    /// To specify a value per consumer, use the <see cref="EventRegistration.IdFormat"/> option.
    /// To specify a value per transport, use the <see cref="EventBusTransportOptionsBase.DefaultEventIdFormat"/> option on the specific transport.
    /// Defaults to <see cref="EventIdFormat.Guid"/>.
    /// </summary>
    public EventIdFormat DefaultEventIdFormat { get; set; } = EventIdFormat.Guid;

    /// <summary>
    /// Optional default retry policy to use for consumers where it is not specified.
    /// To specify a value per consumer, use the <see cref="EventConsumerRegistration.RetryPolicy"/> option.
    /// To specify a value per transport, use the <see cref="EventBusTransportOptionsBase.DefaultConsumerRetryPolicy"/> option on the specific transport.
    /// Defaults to <see langword="null"/>.
    /// </summary>
    public AsyncRetryPolicy? DefaultConsumerRetryPolicy { get; set; }

    /// <summary>
    /// Optional default behaviour for errors encountered in a consumer but are not handled.
    /// To specify a value per consumer, use the <see cref="EventConsumerRegistration.UnhandledErrorBehaviour"/> option.
    /// To specify a value per transport, use the <see cref="EventBusTransportOptionsBase.DefaultUnhandledConsumerErrorBehaviour"/> option on the specific transport.
    /// When an <see cref="AsyncRetryPolicy"/> is in force, only errors that are not handled by it will be subject to the value set here.
    /// Defaults to <see langword="null"/>.
    /// </summary>
    public UnhandledConsumerErrorBehaviour? DefaultUnhandledConsumerErrorBehaviour { get; set; }

    /// <summary>
    /// Gets or sets the name of the default transport.
    /// When there is only one transport registered, setting this value is not necessary, as it is used as the default.
    /// </summary>
    public string? DefaultTransportName { get; set; }

    /// <summary>
    /// The map of registered transport names to their types.
    /// </summary>
    internal Dictionary<string, Type> RegisteredTransportNames { get; } = new Dictionary<string, Type>();

    /// <summary>
    /// The registrations for events and consumers for the EventBus.
    /// </summary>
    internal Dictionary<Type, EventRegistration> Registrations { get; } = new Dictionary<Type, EventRegistration>();

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
    /// Get the consumer registration in a given event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type from which to retrieve a <see cref="EventConsumerRegistration"/> for.</typeparam>
    /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
    /// <param name="reg">
    /// When this method returns, contains the event registration associated with the specified event type,
    /// if the event type is found; otherwise, <see langword="null"/> is returned.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <param name="ecr">
    /// When this method returns, contains the consumer registration associated with the specified event type,
    /// if the event type is found; otherwise, <see langword="null"/> is returned.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
    internal bool TryGetConsumerRegistration<TEvent, TConsumer>([NotNullWhen(true)] out EventRegistration? reg,
                                                                [NotNullWhen(true)] out EventConsumerRegistration? ecr)
    {
        ecr = default;
        if (Registrations.TryGetValue(typeof(TEvent), out reg))
        {
            ecr = reg.Consumers.SingleOrDefault(cr => cr.ConsumerType == typeof(TConsumer));
            if (ecr is not null) return true;
        }
        return false;
    }

    /// <summary>
    /// Get the consumer registration in a given event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type from which to retrieve a <see cref="EventConsumerRegistration"/> for.</typeparam>
    /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
    /// <param name="registration">
    /// When this method returns, contains the consumer registration associated with the specified event type,
    /// if the event type is found; otherwise, <see langword="null"/> is returned.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
    public bool TryGetConsumerRegistration<TEvent, TConsumer>([NotNullWhen(true)] out EventConsumerRegistration? registration)
    {
        return TryGetConsumerRegistration<TEvent, TConsumer>(out _, out registration);
    }

    /// <summary>
    /// Configure the <see cref="EventRegistration"/> for <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event to configure for</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusOptions ConfigureEvent<TEvent>(Action<EventRegistration> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        // if there's already a registration for the event return it
        var eventType = typeof(TEvent);
        if (!Registrations.TryGetValue(key: eventType, out var registration))
        {
            Registrations[eventType] = registration = new EventRegistration(eventType);
        }

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

        if (TryGetConsumerRegistration<TEvent, TConsumer>(out var reg, out var ecr) && ecr is not null)
        {
            configure(reg, ecr);
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
