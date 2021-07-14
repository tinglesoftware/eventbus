using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

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
            registration.SetTransportName(options);

            // set event name and kind
            registration.SetEventName(options.Naming);
            registration.SetEntityKind();

            // set the consumer names
            registration.SetConsumerNames(options.Naming, environment);

            // set the readiness provider
            registration.SetReadinessProviders();
        }
    }
}
