using Microsoft.Extensions.DependencyInjection;
using System;
using Tingle.EventBus;

namespace Microsoft.Extensions.Options
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
            var consumers = busOptions.GetConsumerRegistrations(TransportNames.AzureEventHubs);
            if (consumers.Count > 0)
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
        }
    }
}
