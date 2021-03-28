using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AmazonSqsTransportOptions"/>.
    /// </summary>
    internal class AmazonSqsPostConfigureOptions : IPostConfigureOptions<AmazonSqsTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public AmazonSqsPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        public void PostConfigure(string name, AmazonSqsTransportOptions options)
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

            // Ensure we have options for SQS and SNS and their regions are set
            options.SqsConfig ??= new AmazonSQSConfig();
            options.SqsConfig.RegionEndpoint ??= options.Region;
            options.SnsConfig ??= new AmazonSimpleNotificationServiceConfig();
            options.SnsConfig.RegionEndpoint ??= options.Region;

            // Ensure the entity names are not longer than the limits
            var registrations = busOptions.GetRegistrations(TransportNames.AmazonSqs);
            foreach (var ereg in registrations)
            {
                // Ensure the entity type is allowed
                options.EnsureAllowedEntityKind(ereg, EntityKind.Broadcast, EntityKind.Queue);

                // Event names become Topic names and they should not be longer than 256 characters
                // See https://aws.amazon.com/sns/faqs/#:~:text=Features%20and%20functionality,and%20underscores%20(_)%20are%20allowed.
                if (ereg.EventName.Length > 256)
                {
                    throw new InvalidOperationException($"EventName '{ereg.EventName}' generated from '{ereg.EventType.Name}' is too long. "
                                                       + "Amazon SNS does not allow more than 256 characters for Topic names.");
                }

                // Consumer names become Queue names and they should not be longer than 80 characters
                // See https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/quotas-queues.html
                foreach (var creg in ereg.Consumers)
                {
                    if (creg.ConsumerName.Length > 80)
                    {
                        throw new InvalidOperationException($"ConsumerName '{creg.ConsumerName}' generated from '{creg.ConsumerType.Name}' is too long. "
                                                           + "Amazon SQS does not allow more than 80 characters for Queue names.");
                    }
                }
            }
        }
    }
}
