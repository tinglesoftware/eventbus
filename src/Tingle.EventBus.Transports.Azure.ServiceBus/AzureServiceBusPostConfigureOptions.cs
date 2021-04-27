using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AzureServiceBusTransportOptions"/>.
    /// </summary>
    internal class AzureServiceBusPostConfigureOptions : AzureTransportPostConfigureOptions<AzureServiceBusTransportCredentials, AzureServiceBusTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public AzureServiceBusPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        /// <inheritdoc/>
        public override void PostConfigure(string name, AzureServiceBusTransportOptions options)
        {
            base.PostConfigure(name, options);

            // Ensure the entity names are not longer than the limits
            // See https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas#messaging-quotas
            var registrations = busOptions.GetRegistrations(TransportNames.AzureServiceBus);
            foreach (var ereg in registrations)
            {
                // Set the IdFormat
                options.SetEventIdFormat(ereg, busOptions);

                // Ensure the entity type is allowed
                options.EnsureAllowedEntityKind(ereg, EntityKind.Broadcast, EntityKind.Queue);

                // Event names become Topic and Queue names and they should not be longer than 260 characters
                if (ereg.EventName.Length > 260)
                {
                    throw new InvalidOperationException($"EventName '{ereg.EventName}' generated from '{ereg.EventType.Name}' is too long. "
                                                       + "Azure Service Bus does not allow more than 260 characters for Topic and Queue names.");
                }

                // Consumer names become Subscription names and they should not be longer than 50 characters
                // When not using Queues, ConsumerName -> SubscriptionName does not happen
                if (ereg.EntityKind == EntityKind.Broadcast)
                {
                    foreach (var creg in ereg.Consumers)
                    {
                        if (creg.ConsumerName.Length > 50)
                        {
                            throw new InvalidOperationException($"ConsumerName '{creg.ConsumerName}' generated from '{creg.ConsumerType.Name}' is too long. "
                                                               + "Azure Service Bus does not allow more than 50 characters for Subscription names.");
                        }
                    }
                }
            }
        }
    }
}
