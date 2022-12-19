using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="KafkaTransportOptions"/>.
/// </summary>
internal class KafkaConfigureOptions : TransportOptionsConfigureOptions<KafkaTransportOptions>
{
    private readonly EventBusOptions busOptions;

    /// <summary>
    /// Initializes a new <see cref="KafkaConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public KafkaConfigureOptions(IEventBusConfigurationProvider configurationProvider, IOptions<EventBusOptions> busOptionsAccessor)
        : base(configurationProvider)
    {
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    /// <inheritdoc/>
    protected override void Configure(IConfiguration configuration, KafkaTransportOptions options)
    {
        base.Configure(configuration, options);

        options.CheckpointInterval = configuration.GetValue<int?>(nameof(options.CheckpointInterval)) ?? options.CheckpointInterval;
        var section = configuration.GetSection(nameof(options.BootstrapServers));
        if (section is not null && section.GetChildren().Any())
        {
            section.Bind(options.BootstrapServers);
        }
    }

    /// <inheritdoc/>
    public override void PostConfigure(string? name, KafkaTransportOptions options)
    {
        base.PostConfigure(name, options);

        if (options.BootstrapServers == null && options.AdminConfig == null)
        {
            throw new InvalidOperationException($"Either '{nameof(options.BootstrapServers)}' or '{nameof(options.AdminConfig)}' must be provided");
        }

        if (options.BootstrapServers != null && options.BootstrapServers.Any(b => string.IsNullOrWhiteSpace(b)))
        {
            throw new ArgumentNullException(nameof(options.BootstrapServers), "A bootstrap server cannot be null or empty");
        }

        // ensure we have a config
        options.AdminConfig ??= new AdminClientConfig
        {
            BootstrapServers = options.BootstrapServers is null ? "" : string.Join(",", options.BootstrapServers)
        };

        if (string.IsNullOrWhiteSpace(options.AdminConfig.BootstrapServers))
        {
            throw new InvalidOperationException($"BootstrapServers must be provided via '{nameof(options.BootstrapServers)}' or '{nameof(options.AdminConfig)}'.");
        }

        // ensure the checkpoint interval is not less than 1
        options.CheckpointInterval = Math.Max(options.CheckpointInterval, 1);

        // ensure there's only one consumer per event
        var registrations = busOptions.GetRegistrations(name!);
        var multiple = registrations.FirstOrDefault(r => r.Consumers.Count > 1);
        if (multiple is not null)
        {
            throw new InvalidOperationException($"More than one consumer registered for '{multiple.EventType.Name}' yet "
                                               + "Kafka does not support more than one consumer per event in the same application domain.");
        }

        // Ensure the entity names are not longer than the limits
        // See https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-quotas#common-limits-for-all-tiers
        foreach (var reg in registrations)
        {
            // Set the IdFormat
            options.SetEventIdFormat(reg, busOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast);

            // Event names become Topic names and they should not be longer than 255 characters
            // https://www.ibm.com/support/knowledgecenter/SSMKHH_10.0.0/com.ibm.etools.mft.doc/bz91041_.html
            if (reg.EventName!.Length > 255)
            {
                throw new InvalidOperationException($"EventName '{reg.EventName}' generated from '{reg.EventType.Name}' is too long. "
                                                   + "Kafka does not allow more than 255 characters for Topic names.");
            }

            // Consumer names become Consumer Group IDs and they should not be longer than 255 characters
            foreach (var ecr in reg.Consumers)
            {
                if (ecr.ConsumerName!.Length > 255)
                {
                    throw new InvalidOperationException($"ConsumerName '{ecr.ConsumerName}' generated from '{ecr.ConsumerType.Name}' is too long. "
                                                       + "Kafka does not allow more than 255 characters for Consumer Group IDs.");
                }
            }
        }
    }
}
