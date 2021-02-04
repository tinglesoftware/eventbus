using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Represents a registration for an event.
    /// </summary>
    public class EventRegistration : IEquatable<EventRegistration>
    {
        /// <summary>
        /// Creates an instance of <see cref="EventRegistration"/>.
        /// </summary>
        /// <param name="eventType">The type of event handled.</param>
        public EventRegistration(Type eventType)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        }

        /// <summary>
        /// The type of event handled.
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// The name generated for the event.
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// The name of the transport used for the event.
        /// </summary>
        public string TransportName { get; set; }

        /// <summary>
        /// The type used for serializing and deserializing events.
        /// This type must implement <see cref="IEventSerializer"/>.
        /// </summary>
        public Type EventSerializerType { get; set; }

        /// <summary>
        /// The list of consumers registered for this event.
        /// </summary>
        /// <remarks>
        /// This is backed by a <see cref="HashSet{T}"/> to ensure no duplicates.
        /// </remarks>
        public ICollection<EventConsumerRegistration> Consumers { get; set; } = new HashSet<EventConsumerRegistration>();

        /// <summary>
        /// Gets a key/value collection that can be used to organize and share data across components
        /// of the event bus such as the bus, the transport or the serializer.
        /// </summary>
        public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        internal EventRegistration SetSerializer()
        {
            // If the event serializer has not been specified, attempt to get from the attribute
            var attrs = EventType.GetCustomAttributes(false);
            EventSerializerType ??= attrs.OfType<EventSerializerAttribute>().SingleOrDefault()?.SerializerType;
            EventSerializerType ??= typeof(IEventSerializer); // use the default when not provided

            // Ensure the serializer is either default or it implements IEventSerializer
            if (EventSerializerType != typeof(IEventSerializer)
                && !typeof(IEventSerializer).IsAssignableFrom(EventSerializerType))
            {
                throw new InvalidOperationException($"The type '{EventSerializerType.FullName}' is used as a serializer "
                                                  + $"but does not implement '{typeof(IEventSerializer).FullName}'");
            }

            return this;
        }

        internal EventRegistration SetTransportName(EventBusOptions options)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));

            // If the event transport name has not been specified, attempt to get from the attribute
            var type = EventType;
            TransportName ??= type.GetCustomAttributes(false).OfType<EventTransportNameAttribute>().SingleOrDefault()?.Name;

            // If the event transport name has not been set, try the default one
            TransportName ??= options.DefaultTransportName;

            // Set the transport name from the default, if not set
            if (string.IsNullOrWhiteSpace(TransportName))
            {
                throw new InvalidOperationException($"Unable to set the transport for event '{type.FullName}'."
                                                  + $" Either set the '{nameof(options.DefaultTransportName)}' option"
                                                  + $" or use the '{typeof(EventTransportNameAttribute).FullName}' on the event.");
            }

            // Ensure the transport name set has been registered
            if (!options.RegisteredTransportNames.ContainsKey(TransportName))
            {
                throw new InvalidOperationException($"Transport '{TransportName}' on event '{type.FullName}' must be registered.");
            }

            return this;
        }

        #region Equality Overrides

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as EventRegistration);

        /// <inheritdoc/>
        public bool Equals(EventRegistration other)
        {
            return other != null && EqualityComparer<Type>.Default.Equals(EventType, other.EventType);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => EventType.GetHashCode();

        ///
        public static bool operator ==(EventRegistration left, EventRegistration right)
        {
            return EqualityComparer<EventRegistration>.Default.Equals(left, right);
        }

        ///
        public static bool operator !=(EventRegistration left, EventRegistration right) => !(left == right);

        #endregion
    }
}
