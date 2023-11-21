using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AmazonSqsTransportOptions"/>.
/// </summary>
internal class AmazonSqsConfigureOptions : AmazonTransportConfigureOptions<AmazonSqsTransportOptions>
{
    /// <summary>
    /// Initializes a new <see cref="AmazonSqsConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    /// <param name="configurators">A list of <see cref="IEventBusConfigurator"/> to use when configuring options.</param>
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public AmazonSqsConfigureOptions(IEventBusConfigurationProvider configurationProvider, IEnumerable<IEventBusConfigurator> configurators, IOptions<EventBusOptions> busOptionsAccessor)
        : base(configurationProvider, configurators, busOptionsAccessor) { }

    /// <inheritdoc/>
    public override void PostConfigure(string? name, AmazonSqsTransportOptions options)
    {
        base.PostConfigure(name, options);

        // Ensure we have options for SQS and SNS and their regions are set
        options.SqsConfig ??= new AmazonSQSConfig();
        options.SqsConfig.RegionEndpoint ??= options.Region;
        options.SnsConfig ??= new AmazonSimpleNotificationServiceConfig();
        options.SnsConfig.RegionEndpoint ??= options.Region;

        // Ensure the entity names are not longer than the limits
        var registrations = BusOptions.GetRegistrations(name!);
        foreach (var reg in registrations)
        {
            // Set the values using defaults
            options.SetValuesUsingDefaults(reg, BusOptions);

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
