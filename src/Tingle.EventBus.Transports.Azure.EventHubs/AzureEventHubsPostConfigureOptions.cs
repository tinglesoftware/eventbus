using Microsoft.Extensions.Options;
using System;
using System.Linq;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AzureEventHubsTransportOptions"/>.
    /// </summary>
    internal class AzureEventHubsPostConfigureOptions : IPostConfigureOptions<AzureEventHubsTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public AzureEventHubsPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        public void PostConfigure(string name, AzureEventHubsTransportOptions options)
        {
            // ensure the connection string
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException($"The '{nameof(options.ConnectionString)}' must be provided");
            }

            // if there are consumers for this transport, we must check azure blob storage
            var registrations = busOptions.GetRegistrations(TransportNames.AzureEventHubs);
            if (registrations.Any(r => r.Consumers.Count > 0))
            {
                // ensure the connection string for blob storage is valid
                if (string.IsNullOrWhiteSpace(options.BlobStorageConnectionString))
                {
                    throw new InvalidOperationException($"The '{nameof(options.BlobStorageConnectionString)}' must be provided");
                }

                // ensure the blob container name is provided
                if (string.IsNullOrWhiteSpace(options.BlobContainerName))
                {
                    throw new InvalidOperationException($"The '{nameof(options.BlobContainerName)}' must be provided");
                }

                // ensure the prefix is always lower case.
                options.BlobContainerName = options.BlobContainerName.ToLower();
            }

            // Ensure the entity names are not longer than the limits
            // See https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-quotas#common-limits-for-all-tiers
            foreach (var ereg in registrations)
            {
                // Event names become Event Hub names and they should not be longer than 256 characters
                if (ereg.EventName.Length > 256)
                {
                    throw new InvalidOperationException($"EventName '{ereg.EventName}' generated from '{ereg.EventType.Name}' is too long. "
                                                       + "Azure Event Hubs does not allow more than 256 characters for Event Hub names.");
                }

                // Consumer names become Consumer Group names and they should not be longer than 256 characters
                foreach (var creg in ereg.Consumers)
                {
                    if (creg.ConsumerName.Length > 256)
                    {
                        throw new InvalidOperationException($"ConsumerName '{creg.ConsumerName}' generated from '{creg.ConsumerType.Name}' is too long. "
                                                           + "Azure Event Hubs does not allow more than 256 characters for Consumer Group names.");
                    }
                }
            }
        }
    }
}
