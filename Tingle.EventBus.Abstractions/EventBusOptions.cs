using System;
using System.Collections.Generic;
using Tingle.EventBus.Abstractions.Serialization;

namespace Tingle.EventBus.Abstractions
{
    public class EventBusOptions
    {
        /// <summary>
        /// The options to use for serializations.
        /// </summary>
        public EventBusSerializerOptions SerializerOptions { get; } = new EventBusSerializerOptions();

        /// <summary>
        /// The naming convention to use when generating names for types and entities on the sepected transport.
        /// Defaults to <see cref="EventBusNamingConvention.KebabCase"/>.
        /// </summary>
        public EventBusNamingConvention NamingConvention { get; set; } = EventBusNamingConvention.KebabCase;

        /// <summary>
        /// Determins if to prefix entity names with the application name.
        /// This is important to enable when the events of a similar type are consumed by the multiple services to avoid conflicts.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool PrefixApplicationName { get; set; } = true;


        internal Dictionary<Type, EventConsumerRegistration> EventRegistrations { get; } = new Dictionary<Type, EventConsumerRegistration>();
    }
}
