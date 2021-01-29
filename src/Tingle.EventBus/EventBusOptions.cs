using System;
using System.Collections.Generic;
using System.Text.Json;
using Tingle.EventBus.Registrations;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus
{
    /// <summary>
    /// Options for configuring the event bus irrespective of transport.
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
        /// The naming convention to use when generating names for types and entities on the sepected transport.
        /// Defaults to <see cref="NamingConvention.KebabCase"/>.
        /// </summary>
        public NamingConvention NamingConvention { get; set; } = NamingConvention.KebabCase;

        /// <summary>
        /// The scope to use for queues and subscriptions.
        /// Set to <see langword="null"/> for non-scoped entities.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// Determines if to trim suffixes such as <c>Consumer</c>, <c>Event</c> and <c>EventConsumer</c>
        /// in names generated from type names.
        /// For example <c>DoorOpenedEvent</c> would be trimmed to <c>DoorTrimed</c>,
        /// <c>DoorOpenedEventConsumer</c> would be trimmed to <c>DoorOpened</c>,
        /// <c>DoorOpenedConsumer</c> would be trimmed to <c>DoorOpened</c>.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool TrimTypeNames { get; set; } = true;

        /// <summary>
        /// Determines if to use the full name when generating entity names.
        /// This should always be enabled if there are types with the same names.
        /// For example <see cref="string"/> would produce <c>System.String</c>, <c>system-string</c>,
        /// or <c>system_string</c>; when enabled, otherwise just <c>string</c>.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool UseFullTypeNames { get; set; } = true;

        /// <summary>
        /// The source used to generate names for consumers.
        /// Some transports are versy sensitive to the value used here and thus should be used carefully.
        /// When set to <see cref="ConsumerNameSource.ApplicationName"/>, each subscription/exchange will
        /// be named the same as the application before appending the event name.
        /// For applications with more than one consumer per event type, use <see cref="ConsumerNameSource.TypeName"/>
        /// to avoid duplicates.
        /// Defaults to <see cref="ConsumerNameSource.TypeName"/>.
        /// </summary>
        public ConsumerNameSource ConsumerNameSource { get; set; } = ConsumerNameSource.TypeName;

        /// <summary>
        /// The information about the host where the EventBus is running.
        /// </summary>
        internal HostInfo HostInfo { get; set; }

        /// <summary>
        /// The registrations for events and consumers for the EventBus.
        /// </summary>
        internal Dictionary<Type, EventRegistration> Registrations { get; } = new Dictionary<Type, EventRegistration>();

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
        /// Configure the <see cref="EventRegistration"/> for <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The event to configure for</typeparam>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusOptions ConfigureEvent<TEvent>(Action<EventRegistration> configure)
        {
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var registration = this.GetOrCreateRegistration<TEvent>();
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

            if (this.TryGetConsumerRegistration<TEvent, TConsumer>(out var registration))
            {
                configure(registration);
            }

            return this;
        }

    }
}
