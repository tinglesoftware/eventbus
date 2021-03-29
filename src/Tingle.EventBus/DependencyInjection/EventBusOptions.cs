using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Tingle.EventBus;
using Tingle.EventBus.Registrations;
using Tingle.EventBus.Serialization;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Represents all the transport-agnostic options you can use to configure the Event Bus.
    /// </summary>
    public class EventBusOptions
    {
        /// <summary>
        /// The duration of time to delay the starting of the bus.
        /// The default value is 5 seconds. Max value is 10 minutes and minimum is 5 seconds.
        /// When <see langword="null"/>, the bus is started immediately.
        /// </summary>
        public TimeSpan? StartupDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The options to use for serialization.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                           | System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
                                   | System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// Gets the <see cref="EventBusNamingOptions"/> for the Event Bus.
        /// </summary>
        public EventBusNamingOptions Naming { get; } = new EventBusNamingOptions();

        /// <summary>
        /// The information about the host where the EventBus is running.
        /// </summary>
        public HostInfo HostInfo { get; set; }

        /// <summary>
        /// Indicates if the messages/events procuded require guard against duplicate messages.
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
        /// Optional default retry policy to use for consumers where it is not specified.
        /// To specify a value per consumer, use the <see cref="EventConsumerRegistration.RetryPolicy"/> option.
        /// To specify a value per transport, use the <see cref="EventBusTransportOptionsBase.DefaultConsumerRetryPolicy"/> option on the specific transport.
        /// Defaults to <see langword="null"/>.
        /// </summary>
        public AsyncRetryPolicy DefaultConsumerRetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets the name of the default transport.
        /// When there is only one transport registered, setting this value is not necessary, as it is used as the default.
        /// </summary>
        public string DefaultTransportName { get; set; }

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
        /// Get or create the event registration for a given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to retrieve an <see cref="EventRegistration"/> for.</typeparam>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">The given event type does not have any event registered.</exception>
        public EventRegistration GetOrCreateRegistration<TEvent>()
        {
            // if there's already a registration for the event return it
            var eventType = typeof(TEvent);
            if (Registrations.TryGetValue(key: eventType, out var registration)) return registration;

            // at this point, the registration does not exist;
            // create it and add to the registrations for repeated use
            registration = new EventRegistration(eventType);
            registration.SetSerializer() // set serializer
                        .SetEventName(Naming) // set event name
                        .SetEntityKind()
                        .SetTransportName(this); // set transport name
            return Registrations[eventType] = registration;
        }

        /// <summary>
        /// Get the consumer registration in a given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type from wich to retrieve a <see cref="EventConsumerRegistration"/> for.</typeparam>
        /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
        /// <param name="registration">
        /// When this method returns, contains the consumer registration associated with the specified event type,
        /// if the event type is found; otherwise, <see langword="null"/> is returned.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
        public bool TryGetConsumerRegistration<TEvent, TConsumer>(out EventConsumerRegistration registration)
        {
            registration = default;
            if (Registrations.TryGetValue(typeof(TEvent), out var ereg))
            {
                registration = ereg.Consumers.SingleOrDefault(cr => cr.ConsumerType == typeof(TConsumer));
                if (registration != null) return true;
            }
            return false;
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

            var registration = GetOrCreateRegistration<TEvent>();
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
        public EventBusOptions ConfigureConsumer<TEvent, TConsumer>(Action<EventConsumerRegistration> configure)
            where TConsumer : class, IEventConsumer
        {
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            if (TryGetConsumerRegistration<TEvent, TConsumer>(out var registration))
            {
                configure(registration);
            }

            return this;
        }
    }
}
