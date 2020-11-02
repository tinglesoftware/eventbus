using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus.Abstractions.Serialization;

namespace Tingle.EventBus.Abstractions
{
    public class EventBusOptions
    {
        /// <summary>
        /// The options to use for serializations.
        /// </summary>
        public EventSerializerOptions SerializerOptions { get; } = new EventSerializerOptions();

        /// <summary>
        /// The naming convention to use when generating names for types and entities on the sepected transport.
        /// Defaults to <see cref="EventBusNamingConvention.KebabCase"/>.
        /// </summary>
        public EventBusNamingConvention NamingConvention { get; set; } = EventBusNamingConvention.KebabCase;

        /// <summary>
        /// Determines if to use the full name when generating entity names.
        /// This should always be enabled if there are types with the same names.
        /// For example <see cref="string"/> would produce <c>System.String</c>, <c>system-string</c>,
        /// or <c>system_string</c>; when enabled otherwise just <c>String</c>.
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

        internal Dictionary<Type, Type> EventRegistrations { get; } = new Dictionary<Type, Type>();

        public ICollection<EventConsumerRegistration> GetRegistrations()
        {
            return EventRegistrations.Select(r => new EventConsumerRegistration(r.Key, r.Value)).ToList();
        }
    }
}
