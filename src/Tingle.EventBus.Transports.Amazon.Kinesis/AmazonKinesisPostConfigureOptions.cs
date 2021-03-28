using Amazon;
using Amazon.Kinesis;
using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AmazonKinesisTransportOptions"/>.
    /// </summary>
    internal class AmazonKinesisPostConfigureOptions : IPostConfigureOptions<AmazonKinesisTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public AmazonKinesisPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        public void PostConfigure(string name, AmazonKinesisTransportOptions options)
        {
            // Ensure the region is provided
            if (string.IsNullOrWhiteSpace(options.RegionName) && options.Region == null)
            {
                throw new InvalidOperationException($"Either '{nameof(options.RegionName)}' or '{nameof(options.Region)}' must be provided");
            }

            options.Region ??= RegionEndpoint.GetBySystemName(options.RegionName);

            // Ensure the access key is specified
            if (string.IsNullOrWhiteSpace(options.AccessKey))
            {
                throw new InvalidOperationException($"The '{nameof(options.AccessKey)}' must be provided");
            }

            // Ensure the secret is specified
            if (string.IsNullOrWhiteSpace(options.SecretKey))
            {
                throw new InvalidOperationException($"The '{nameof(options.SecretKey)}' must be provided");
            }

            // Ensure we have options for Kinesis and the region is set
            options.KinesisConfig ??= new AmazonKinesisConfig();
            options.KinesisConfig.RegionEndpoint ??= options.Region;

            // Ensure the partition key resolver is set
            if (options.PartitionKeyResolver == null)
            {
                throw new InvalidOperationException($"The '{nameof(options.PartitionKeyResolver)}' must be provided");
            }

            // Ensure the entity names are not longer than the limits
            var registrations = busOptions.GetRegistrations(TransportNames.AmazonKinesis);
            foreach (var ereg in registrations)
            {
                // Ensure the entity type is allowed
                options.EnsureAllowedEntityKind(ereg, EntityKind.Stream);

                // Event names become Stream names and they should not be longer than 128 characters
                // See https://docs.aws.amazon.com/kinesis/latest/APIReference/API_CreateStream.html
                if (ereg.EventName.Length > 128)
                {
                    throw new InvalidOperationException($"EventName '{ereg.EventName}' generated from '{ereg.EventType.Name}' is too long. "
                                                       + "Amazon Kinesis does not allow more than 128 characters for Stream names.");
                }

                // Consumer names become Queue names and they should not be longer than 128 characters
                // See https://docs.aws.amazon.com/kinesis/latest/APIReference/API_RegisterStreamConsumer.html
                foreach (var creg in ereg.Consumers)
                {
                    if (creg.ConsumerName.Length > 128)
                    {
                        throw new InvalidOperationException($"ConsumerName '{creg.ConsumerName}' generated from '{creg.ConsumerType.Name}' is too long. "
                                                           + "Amazon Kinesis does not allow more than 128 characters for Stream Consumer names.");
                    }
                }
            }
        }
    }
}
