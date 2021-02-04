using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AzureServiceBusTransportOptions"/>.
    /// </summary>
    internal class AzureServiceBusPostConfigureOptions : IPostConfigureOptions<AzureServiceBusTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public AzureServiceBusPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        public void PostConfigure(string name, AzureServiceBusTransportOptions options)
        {
            // Ensure the connection string is not null
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException($"The '{nameof(options.ConnectionString)}' must be provided");
            }

            // Ensure the entity names are not longer than the limits
            // See https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas#messaging-quotas
            var registrations = busOptions.GetRegistrations(TransportNames.AzureServiceBus);
            foreach (var ereg in registrations)
            {
                // Event names become Topic and Queue names and they should not be longer than 260 characters
                if (ereg.EventName.Length > 260)
                {
                    throw new InvalidOperationException($"EventName '{ereg.EventName}' generated from '{ereg.EventType.Name}' is too long. "
                                                       + "Azure Service Bus does not allow more than 260 characters for Topic and Queue names.");
                }

                // Consumer names become Subscription names and they should not be longer than 50 characters                
                if (!options.UseBasicTier) // When not using basic tier, ConsumerName -> SubscriptionName does not happen
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
