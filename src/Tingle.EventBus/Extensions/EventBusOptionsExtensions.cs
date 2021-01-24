using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus
{
    /// <summary>
    /// Extension methods on <see cref="EventBusOptions"/>.
    /// </summary>
    public static class EventBusOptionsExtensions
    {
        /// <summary>
        /// Get or create the event registration for a given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to retrieve an <see cref="EventRegistration"/> for.</typeparam>
        /// <param name="options">The instance of <see cref="EventBusOptions"/> to use.</param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">The given event type does not have any event registered.</exception>
        public static EventRegistration GetOrCreateRegistration<TEvent>(this EventBusOptions options)
        {
            // if there's already a registration for the event return it
            var eventType = typeof(TEvent);
            if (options.Registrations.TryGetValue(key: eventType, out var registration)) return registration;

            // at this point, the registration does not exist; create it and add to the registrations for repeated use
            registration = new EventRegistration(eventType);
            registration.SetSerializer() // set serializer
                        .SetEventName(options) // set event name
                        .SetTransportName(options); // set transport name
            return options.Registrations[eventType] = registration;
        }

        /// <summary>
        /// Gets the consumer registrations for a given transport.
        /// </summary>
        /// <param name="options">The instance of <see cref="EventBusOptions"/> to use.</param>
        /// <param name="transportName">The name of the transport for whom to get registered consumers.</param>
        /// <returns></returns>
        public static ICollection<EventRegistration> GetRegistrations(this EventBusOptions options, string transportName)
        {
            if (string.IsNullOrWhiteSpace(transportName))
            {
                throw new ArgumentException($"'{nameof(transportName)}' cannot be null or whitespace", nameof(transportName));
            }

            // filter out the consumers where the event is set for the given transport
            return options.Registrations.Values.Where(r => r.TransportName == transportName).ToList();
        }

        /// <summary>
        /// Get the consumer registration in a given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type from wich to retrieve a <see cref="EventConsumerRegistration"/> for.</typeparam>
        /// <typeparam name="TConsumer">The consumer to configure.</typeparam>
        /// <param name="options">The instance of <see cref="EventBusOptions"/> to use.</param>
        /// <param name="registration">
        /// When this method returns, contains the consumer registration associated with the specified event type,
        /// if the event type is found; otherwise, <see langword="null"/> is returned.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
        public static bool TryGetConsumerRegistration<TEvent, TConsumer>(this EventBusOptions options, out EventConsumerRegistration registration)
        {
            registration = default;
            if (options.Registrations.TryGetValue(typeof(TEvent), out var ereg))
            {
                registration = ereg.Consumers.SingleOrDefault(cr => cr.ConsumerType == typeof(TConsumer));
                if (registration != null) return true;
            }
            return false;
        }
    }
}
