using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Specifies options for naming behaviour and requirements.
    /// </summary>
    public class EventBusNamingOptions
    {
        /// <summary>
        /// The scope to use for queues and subscriptions.
        /// Set to <see langword="null"/> to disable scoping of entities.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// The naming convention to use when generating names for types and entities on the sepected transport.
        /// Defaults to <see cref="NamingConvention.KebabCase"/>.
        /// </summary>
        public NamingConvention Convention { get; set; } = NamingConvention.KebabCase;

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
        /// When set to <see cref="ConsumerNameSource.Prefix"/>, each subscription/exchange will
        /// be named the same as <see cref="ConsumerNamePrefix"/> before appending the event name.
        /// For applications with more than one consumer per event type, use <see cref="ConsumerNameSource.TypeName"/>
        /// to avoid duplicates.
        /// Defaults to <see cref="ConsumerNameSource.TypeName"/>.
        /// </summary>
        public ConsumerNameSource ConsumerNameSource { get; set; } = ConsumerNameSource.TypeName;

        /// <summary>
        /// The prefix used with <see cref="ConsumerNameSource.Prefix"/> and <see cref="ConsumerNameSource.PrefixAndTypeName"/>.
        /// Defaults to <see cref="Hosting.IHostEnvironment.ApplicationName"/>.
        /// </summary>
        public string ConsumerNamePrefix { get; set; }

        /// <summary>
        /// Indicates if the consumer name generated should be suffixed with the event name.
        /// Some transports require this value to be <see langword="true"/>.
        /// Setting to false can reduce the length of the consumer name hence being useful in scenarios
        /// where the transport allow same name for two consumers so long as they are for different events.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool SuffixConsumerName { get; set; } = true;
    }
}
