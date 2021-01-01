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
        /// When specified, the value must be more than 5 seconds but less than 10 minutes.
        /// </summary>
        public TimeSpan? StartupDelay { get; set; }

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
        /// Determines if to use the full name when generating entity names.
        /// This should always be enabled if there are types with the same names.
        /// For example <see cref="string"/> would produce <c>System.String</c>, <c>system-string</c>,
        /// or <c>system_string</c>; when enabled otherwise just <c>string</c>.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool UseFullTypeNames { get; set; } = true;

        ///// <summary>
        ///// Determines if to prefix entity names with the application name.
        ///// This is important to enable when the events of a similar type are consumed by the multiple services to avoid conflicts.
        ///// Defaults to <see langword="true"/>
        ///// </summary>
        //public bool PrefixApplicationName { get; set; } = true;

        /// <summary>
        /// Determines if to use the application name for subscriptions and exchanges instead of the name of the consumer.
        /// When set to true, each subscription to an event will be named the same as the application name, otherwise,
        /// the name of the consumer is used.
        /// <br />
        /// This should always be true if there are multiple consumers in one application for the same event so as not to have same name issues.
        /// Defaults to <see langword="false"/>
        /// </summary>
        public bool UseApplicationNameInsteadOfConsumerName { get; set; } = false;

        /// <summary>
        /// The information about the host where the EventBus is running.
        /// </summary>
        internal HostInfo HostInfo { get; set; }

        /// <summary>
        /// Determines if the consumer name must be used in place of the application name.
        /// This setting overrides <see cref="UseApplicationNameInsteadOfConsumerName"/> and is very critical
        /// to the behaviour for some brokers/transports. Avoid changing it unless you know what you are doing.
        /// </summary>
        public bool ForceConsumerName { get; set; }

        internal Dictionary<Type, EventRegistration> EventRegistrations { get; } = new Dictionary<Type, EventRegistration>();
        internal Dictionary<Type, ConsumerRegistration> ConsumerRegistrations { get; } = new Dictionary<Type, ConsumerRegistration>();

        /// <summary>
        /// Gets or sets the name of the default transport.
        /// When there is only one transport registered, setting this value is not necessary, as it is used as the default.
        /// </summary>
        public string DefaultTransportName { get; set; }

        /// <summary>
        /// The list of registered transport names.
        /// </summary>
        internal Dictionary<string, Type> RegisteredTransportNames { get; } = new Dictionary<string, Type>();

        /// <summary>
        /// Indicates if the messages/events procuded require guard against duplicate messages.
        /// If <see langword="true"/>, duplicate messages having the same <see cref="EventContext.EventId"/>
        /// sent to the same destination within a duration of <see cref="DuplicateDetectionDuration"/> will be discarded.
        /// </summary>
        /// <remarks>
        /// Duplicate detection can only be done on the transport layer because it requires peristent storage.
        /// This feature only works if the transport a message is sent on supports duplicate detection.
        /// </remarks>
        public bool EnableDeduplication { get; set; }

        /// <summary>
        /// The <see cref="TimeSpan"/> duration of duplicate detection history that is maintained by a transport.
        /// </summary>
        /// <remarks>
        /// The default value is 1 minute. Max value is 7 days and minimum is 20 seconds.
        /// This value is only relevant if <see cref="EnableDeduplication"/> is set to <see langword="true"/>.
        /// </remarks>
        public TimeSpan DuplicateDetectionDuration { get; set; } = TimeSpan.FromMinutes(1);
    }
}
