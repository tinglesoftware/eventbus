using Microsoft.Extensions.Options;
using System;
using System.Linq;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AzureQueueStorageTransportOptions"/>.
    /// </summary>
    internal class AzureQueueStoragePostConfigureOptions : AzureTransportPostConfigureOptions<AzureQueueStorageTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public AzureQueueStoragePostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        /// <inheritdoc/>
        public override void PostConfigure(string name, AzureQueueStorageTransportOptions options)
        {
            base.PostConfigure(name, options);

            // Ensure the connection string
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException($"The '{nameof(options.ConnectionString)}' must be provided");
            }

            // Ensure there's only one consumer per event
            var registrations = busOptions.GetRegistrations(TransportNames.AzureQueueStorage);
            var multiple = registrations.FirstOrDefault(r => r.Consumers.Count > 1);
            if (multiple != null)
            {
                throw new InvalidOperationException($"More than one consumer registered for '{multiple.EventType.Name}' yet "
                                                   + "Azure Queue Storage does not support more than one consumer per event in the same application domain.");
            }

            // Ensure the entity names are not longer than the limits
            // See https://docs.microsoft.com/en-us/rest/api/storageservices/naming-queues-and-metadata#queue-names
            foreach (var ereg in registrations)
            {
                // Set the IdFormat
                options.SetEventIdFormat(ereg, busOptions);

                // Ensure the entity type is allowed
                options.EnsureAllowedEntityKind(ereg, EntityKind.Queue);

                // Event names become topic names and they should not be longer than 63 characters
                if (ereg.EventName.Length > 63)
                {
                    throw new InvalidOperationException($"EventName '{ereg.EventName}' generated from '{ereg.EventType.Name}' is too long. "
                                                       + "Azure Queue Storage does not allow more than 63 characters for Queue names.");
                }
            }
        }
    }
}
