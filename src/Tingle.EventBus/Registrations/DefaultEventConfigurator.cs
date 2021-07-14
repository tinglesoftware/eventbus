using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Default implementation of <see cref="IEventConfigurator"/>.
    /// </summary>
    internal class DefaultEventConfigurator : IEventConfigurator
    {
        private readonly IHostEnvironment environment;

        public DefaultEventConfigurator(IHostEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <inheritdoc/>
        public void Configure(EventRegistration registration, EventBusOptions options)
        {
            // set serializer
            registration.SetSerializer();

            // set transport name
            SetTransportName(registration, options);

            // set event name and kind
            registration.SetEventName(options.Naming);
            registration.SetEntityKind();

            // set the consumer names
            registration.SetConsumerNames(options.Naming, environment);

            // set the readiness provider
            registration.SetReadinessProviders();
        }

        private void SetTransportName(EventRegistration registration, EventBusOptions options)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));

            // If the event transport name has not been specified, attempt to get from the attribute
            var type = registration.EventType;
            registration.TransportName ??= type.GetCustomAttributes(false).OfType<EventTransportNameAttribute>().SingleOrDefault()?.Name;

            // If the event transport name has not been set, try the default one
            registration.TransportName ??= options.DefaultTransportName;

            // Set the transport name from the default, if not set
            if (string.IsNullOrWhiteSpace(registration.TransportName))
            {
                throw new InvalidOperationException($"Unable to set the transport for event '{type.FullName}'."
                                                  + $" Either set the '{nameof(options.DefaultTransportName)}' option"
                                                  + $" or use the '{typeof(EventTransportNameAttribute).FullName}' on the event.");
            }

            // Ensure the transport name set has been registered
            if (!options.RegisteredTransportNames.ContainsKey(registration.TransportName))
            {
                throw new InvalidOperationException($"Transport '{registration.TransportName}' on event '{type.FullName}' must be registered.");
            }
        }
    }
}
