using Amazon.Kinesis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AmazonKinesisTransportOptions"/>.
/// </summary>
internal class AmazonKinesisConfigureOptions : AmazonTransportConfigureOptions<AmazonKinesisTransportOptions>, IConfigureNamedOptions<AmazonKinesisTransportOptions>
{
    private readonly IEventBusConfigurationProvider configurationProvider;
    private readonly EventBusOptions busOptions;

    /// <summary>
    /// Initializes a new <see cref="AmazonKinesisConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public AmazonKinesisConfigureOptions(IEventBusConfigurationProvider configurationProvider, IOptions<EventBusOptions> busOptionsAccessor)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    /// <inheritdoc/>
    public void Configure(string? name, AmazonKinesisTransportOptions options)
    {
        if (string.IsNullOrEmpty(name)) return;

        var configSection = configurationProvider.GetTransportConfiguration(name, "AmazonKinesis");
        if (configSection is null || !configSection.GetChildren().Any()) return;

        options.RegionName = configSection.GetValue<string?>(nameof(options.RegionName)) ?? options.RegionName;
        options.AccessKey = configSection.GetValue<string?>(nameof(options.AccessKey)) ?? options.AccessKey;
        options.SecretKey = configSection.GetValue<string?>(nameof(options.SecretKey)) ?? options.SecretKey;
    }

    /// <inheritdoc/>
    public void Configure(AmazonKinesisTransportOptions options) => Configure(Options.Options.DefaultName, options);

    /// <inheritdoc/>
    public override void PostConfigure(string? name, AmazonKinesisTransportOptions options)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));

        base.PostConfigure(name, options);

        // Ensure we have options for Kinesis and the region is set
        options.KinesisConfig ??= new AmazonKinesisConfig();
        options.KinesisConfig.RegionEndpoint ??= options.Region;

        // Ensure the partition key resolver is set
        if (options.PartitionKeyResolver == null)
        {
            throw new InvalidOperationException($"The '{nameof(options.PartitionKeyResolver)}' must be provided");
        }

        // Ensure the entity names are not longer than the limits
        var registrations = busOptions.GetRegistrations(name);
        foreach (var reg in registrations)
        {
            // Set the IdFormat
            options.SetEventIdFormat(reg, busOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast);

            // Event names become Stream names and they should not be longer than 128 characters
            // See https://docs.aws.amazon.com/kinesis/latest/APIReference/API_CreateStream.html
            if (reg.EventName!.Length > 128)
            {
                throw new InvalidOperationException($"EventName '{reg.EventName}' generated from '{reg.EventType.Name}' is too long. "
                                                   + "Amazon Kinesis does not allow more than 128 characters for Stream names.");
            }

            // Consumer names become Queue names and they should not be longer than 128 characters
            // See https://docs.aws.amazon.com/kinesis/latest/APIReference/API_RegisterStreamConsumer.html
            foreach (var ecr in reg.Consumers)
            {
                if (ecr.ConsumerName!.Length > 128)
                {
                    throw new InvalidOperationException($"ConsumerName '{ecr.ConsumerName}' generated from '{ecr.ConsumerType.Name}' is too long. "
                                                       + "Amazon Kinesis does not allow more than 128 characters for Stream Consumer names.");
                }
            }
        }
    }
}
