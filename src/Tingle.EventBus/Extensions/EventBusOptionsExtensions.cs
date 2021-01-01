using System.Collections.Generic;
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
        public static EventRegistration GetOrCreateEventRegistration<TEvent>(this EventBusOptions options)
        {
            var key = typeof(TEvent);

            // if there's already a registration for the event return it
            if (options.EventRegistrations.TryGetValue(key: key, out var registration)) return registration;

            // if there's a registration for the consumer with the same event, return it
            if (options.TryGetConsumerRegistration<TEvent>(out var c_reg)) return c_reg;

            // at this point, the registration does not exist; create it and add to the registrations
            registration = new EventRegistration(key);
            registration.SetSerializer() // set serializer
                        .SetEventName(options) // set event name
                        .SetTransportName(options); // set transport name
            return options.EventRegistrations[key] = registration;
        }

        /// <summary>
        /// Gets all the consumer registrations.
        /// </summary>
        /// <param name="options">The instance of <see cref="EventBusOptions"/> to use.</param>
        /// <returns></returns>
        public static ICollection<ConsumerRegistration> GetConsumerRegistrations(this EventBusOptions options)
        {
            return options.ConsumerRegistrations.Values;
        }

        /// <summary>
        /// Get the consumer registration for a given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to retrieve a <see cref="ConsumerRegistration"/> for.</typeparam>
        /// <param name="options">The instance of <see cref="EventBusOptions"/> to use.</param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">The given event type does not have any consumer registered.</exception>
        public static ConsumerRegistration GetConsumerRegistration<TEvent>(this EventBusOptions options)
        {
            return options.ConsumerRegistrations[typeof(TEvent)];
        }

        /// <summary>
        /// Get the consumer registration for a given event type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to retrieve a <see cref="ConsumerRegistration"/> for.</typeparam>
        /// <param name="options">The instance of <see cref="EventBusOptions"/> to use.</param>
        /// <param name="registration">
        /// When this method returns, contains the consumer registration associated with the specified event type,
        /// if the event type is found; otherwise, <see langword="null"/> is returned.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns><see langword="true" /> if there's a consumer registered for the given event type; otherwise, false.</returns>
        public static bool TryGetConsumerRegistration<TEvent>(this EventBusOptions options, out ConsumerRegistration registration)
        {
            return options.ConsumerRegistrations.TryGetValue(key: typeof(TEvent), out registration);
        }
    }
}
