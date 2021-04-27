using Microsoft.Extensions.Options;
using System;
using System.Linq;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AzureEventHubsTransportOptions"/>.
    /// </summary>
    internal class AzureEventHubsPostConfigureOptions : AzureTransportPostConfigureOptions<AzureEventHubsTransportCredentials, AzureEventHubsTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public AzureEventHubsPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        /// <inheritdoc/>
        public override void PostConfigure(string name, AzureEventHubsTransportOptions options)
        {
            base.PostConfigure(name, options);

            // ensure we have a FullyQualifiedNamespace when using AzureEventHubsTransportCredentials
            if (options.Credentials.Value is AzureEventHubsTransportCredentials aehtc && aehtc.FullyQualifiedNamespace is null)
            {
                throw new InvalidOperationException($"'{nameof(AzureEventHubsTransportCredentials.FullyQualifiedNamespace)}' must be provided when using '{nameof(AzureEventHubsTransportCredentials)}'.");
            }

            // If there are consumers for this transport, we must check azure blob storage
            var registrations = busOptions.GetRegistrations(TransportNames.AzureEventHubs);
            if (registrations.Any(r => r.Consumers.Count > 0))
            {
                // ensure the connection string for blob storage or token credential is provided
                if (options.BlobStorageCredentials is null || options.BlobStorageCredentials.Value is null)
                {
                    throw new InvalidOperationException($"'{nameof(options.BlobStorageCredentials)}' must be provided in form a connection string or an instance of '{nameof(AzureBlobStorageCredenetial)}'.");
                }

                // ensure we have a BlobServiceUrl when using AzureBlobStorageCredenetial
                if (options.BlobStorageCredentials.Value is AzureBlobStorageCredenetial absc && absc.BlobServiceUrl is null)
                {
                    throw new InvalidOperationException($"'{nameof(AzureBlobStorageCredenetial.BlobServiceUrl)}' must be provided when using '{nameof(AzureBlobStorageCredenetial)}'.");
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
                // Set the IdFormat
                options.SetEventIdFormat(ereg, busOptions);

                // Ensure the entity type is allowed
                options.EnsureAllowedEntityKind(ereg, EntityKind.Broadcast);

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
