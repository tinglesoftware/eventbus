using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AmazonSqsTransportOptions"/>.
/// </summary>
internal class AmazonSqsPostConfigureOptions : AmazonTransportPostConfigureOptions<AmazonSqsTransportOptions>
{
    private readonly EventBusOptions busOptions;

    public AmazonSqsPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
    {
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    public override void PostConfigure(string name, AmazonSqsTransportOptions options)
    {
        base.PostConfigure(name, options);

        // Ensure we have options for SQS and SNS and their regions are set
        options.SqsConfig ??= new AmazonSQSConfig();
        options.SqsConfig.RegionEndpoint ??= options.Region;
        options.SnsConfig ??= new AmazonSimpleNotificationServiceConfig();
        options.SnsConfig.RegionEndpoint ??= options.Region;

        // Ensure the entity names are not longer than the limits
        var registrations = busOptions.GetRegistrations(TransportNames.AmazonSqs);
        foreach (var reg in registrations)
        {
            // Set the IdFormat
            options.SetEventIdFormat(reg, busOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast, EntityKind.Queue);

            // Event names become Topic names and they should not be longer than 256 characters
            // See https://aws.amazon.com/sns/faqs/#:~:text=Features%20and%20functionality,and%20underscores%20(_)%20are%20allowed.
            if (reg.EventName!.Length > 256)
            {
                throw new InvalidOperationException($"EventName '{reg.EventName}' generated from '{reg.EventType.Name}' is too long. "
                                                   + "Amazon SNS does not allow more than 256 characters for Topic names.");
            }

            // Consumer names become Queue names and they should not be longer than 80 characters
            // See https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/quotas-queues.html
            foreach (var ecr in reg.Consumers)
            {
                if (ecr.ConsumerName!.Length > 80)
                {
                    throw new InvalidOperationException($"ConsumerName '{ecr.ConsumerName}' generated from '{ecr.ConsumerType.Name}' is too long. "
                                                       + "Amazon SQS does not allow more than 80 characters for Queue names.");
                }
            }
        }
    }
}
