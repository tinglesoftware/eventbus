using System;
using System.Collections.Generic;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus
{
    /// <summary>
    /// Options for configuring the event bus irrespective of transport.
    /// </summary>
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

        internal Dictionary<Type, ConsumerRegistration> ConsumerRegistrations { get; } = new Dictionary<Type, ConsumerRegistration>();

        /// <summary>
        /// Gets all the consumer registrations.
        /// </summary>
        /// <returns></returns>
        public ICollection<ConsumerRegistration> GetConsumerRegistrations() => ConsumerRegistrations.Values;

        /// <summary>
        /// Get the consumer registration for a given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to retrieve a <see cref="ConsumerRegistration"/> for.</typeparam>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">The given event type does not have any consumer registered.</exception>
        public ConsumerRegistration GetConsumerRegistration<TEvent>() => ConsumerRegistrations[typeof(TEvent)];

        /// <summary>
        /// Get the consumer registration for a given event type
        /// </summary>
        /// <typeparam name="TEvent">The event type to retrieve a <see cref="ConsumerRegistration"/> for.</typeparam>
        /// <param name="registration">
        /// When this method returns, contains the consumer registration associated with the specified event type,
        /// if the event type is found; otherwise, <see langword="null"/> is returned.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
        public bool TryGetConsumerRegistration<TEvent>(out ConsumerRegistration registration)
        {
            return ConsumerRegistrations.TryGetValue(key: typeof(TEvent), out registration);
        }
    }
}
