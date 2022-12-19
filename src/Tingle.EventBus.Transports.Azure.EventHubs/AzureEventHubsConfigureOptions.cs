using Azure.Messaging.EventHubs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureEventHubsTransportOptions"/>.
/// </summary>
internal class AzureEventHubsConfigureOptions : AzureTransportConfigureOptions<AzureEventHubsTransportCredentials, AzureEventHubsTransportOptions>,
                                                IConfigureNamedOptions<AzureEventHubsTransportOptions>
{
    private readonly IEventBusConfigurationProvider configurationProvider;
    private readonly EventBusOptions busOptions;

    /// <summary>
    /// Initializes a new <see cref="AzureEventHubsConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public AzureEventHubsConfigureOptions(IEventBusConfigurationProvider configurationProvider, IOptions<EventBusOptions> busOptionsAccessor)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    /// <inheritdoc/>
    public void Configure(string? name, AzureEventHubsTransportOptions options)
    {
        if (string.IsNullOrEmpty(name)) return;

        var configSection = configurationProvider.GetTransportConfiguration(name);
        if (configSection is null || !configSection.GetChildren().Any()) return;

        options.BlobContainerName = configSection.GetValue<string?>(nameof(options.BlobContainerName)) ?? options.BlobContainerName;
        options.TransportType = configSection.GetValue<EventHubsTransportType?>(nameof(options.TransportType)) ?? options.TransportType;
        options.UseBasicTier = configSection.GetValue<bool?>(nameof(options.UseBasicTier)) ?? options.UseBasicTier;
        options.CheckpointInterval = configSection.GetValue<int?>(nameof(options.CheckpointInterval)) ?? options.CheckpointInterval;

        var fullyQualifiedNamespace = configSection.GetValue<string?>(nameof(AzureEventHubsTransportCredentials.FullyQualifiedNamespace))
                                   ?? configSection.GetValue<string?>("Namespace");
        options.Credentials = fullyQualifiedNamespace is not null
            ? new AzureEventHubsTransportCredentials { FullyQualifiedNamespace = fullyQualifiedNamespace, }
            : (configSection.GetValue<string?>("ConnectionString") ?? options.Credentials);

        var serviceUrl = configSection.GetValue<Uri?>("BlobStorage" + nameof(AzureBlobStorageCredentials.ServiceUrl))
                      ?? configSection.GetValue<Uri?>("BlobStorageEndpoint");
        options.BlobStorageCredentials = serviceUrl is not null
            ? new AzureBlobStorageCredentials { ServiceUrl = serviceUrl, }
            : (configSection.GetValue<string?>("BlobStorageConnectionString") ?? options.Credentials);

        options.BlobContainerName = configSection.GetValue<string?>("BlobStorageContainerName") ?? options.BlobContainerName;
    }

    /// <inheritdoc/>
    public void Configure(AzureEventHubsTransportOptions options) => Configure(Options.Options.DefaultName, options);

    /// <inheritdoc/>
    public override void PostConfigure(string? name, AzureEventHubsTransportOptions options)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));

        base.PostConfigure(name, options);

        // ensure we have a FullyQualifiedNamespace when using AzureEventHubsTransportCredentials
        if (options.Credentials.CurrentValue is AzureEventHubsTransportCredentials aehtc && aehtc.FullyQualifiedNamespace is null)
        {
            throw new InvalidOperationException($"'{nameof(AzureEventHubsTransportCredentials.FullyQualifiedNamespace)}' must be provided when using '{nameof(AzureEventHubsTransportCredentials)}'.");
        }

        // ensure the checkpoint interval is not less than 1
        options.CheckpointInterval = Math.Max(options.CheckpointInterval, 1);

        // If there are consumers for this transport, we must check azure blob storage
        var registrations = busOptions.GetRegistrations(name);
        if (registrations.Any(r => r.Consumers.Count > 0))
        {
            // ensure the connection string for blob storage or token credential is provided
            if (options.BlobStorageCredentials == default || options.BlobStorageCredentials.CurrentValue is null)
            {
                throw new InvalidOperationException($"'{nameof(options.BlobStorageCredentials)}' must be provided in form a connection string or an instance of '{nameof(AzureBlobStorageCredentials)}'.");
            }

            // ensure we have a BlobServiceUrl when using AzureBlobStorageCredential
            if (options.BlobStorageCredentials.CurrentValue is AzureBlobStorageCredentials absc && absc.ServiceUrl is null)
            {
                throw new InvalidOperationException($"'{nameof(AzureBlobStorageCredentials.ServiceUrl)}' must be provided when using '{nameof(AzureBlobStorageCredentials)}'.");
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
        foreach (var reg in registrations)
        {
            // Set the IdFormat
            options.SetEventIdFormat(reg, busOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast);

            // Event names become Event Hub names and they should not be longer than 256 characters
            if (reg.EventName!.Length > 256)
            {
                throw new InvalidOperationException($"EventName '{reg.EventName}' generated from '{reg.EventType.Name}' is too long. "
                                                   + "Azure Event Hubs does not allow more than 256 characters for Event Hub names.");
            }

            // Consumer names become Consumer Group names and they should not be longer than 256 characters
            foreach (var ecr in reg.Consumers)
            {
                if (ecr.ConsumerName!.Length > 256)
                {
                    throw new InvalidOperationException($"ConsumerName '{ecr.ConsumerName}' generated from '{ecr.ConsumerType.Name}' is too long. "
                                                       + "Azure Event Hubs does not allow more than 256 characters for Consumer Group names.");
                }
            }
        }
    }
}
