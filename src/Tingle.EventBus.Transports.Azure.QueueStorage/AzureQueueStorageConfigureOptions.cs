using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureQueueStorageTransportOptions"/>.
/// </summary>
internal class AzureQueueStorageConfigureOptions : AzureTransportConfigureOptions<AzureQueueStorageTransportCredentials, AzureQueueStorageTransportOptions>,
                                                   IConfigureNamedOptions<AzureQueueStorageTransportOptions>
{
    private readonly IEventBusConfigurationProvider configurationProvider;
    private readonly EventBusOptions busOptions;

    /// <summary>
    /// Initializes a new <see cref="AzureQueueStorageConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public AzureQueueStorageConfigureOptions(IEventBusConfigurationProvider configurationProvider, IOptions<EventBusOptions> busOptionsAccessor)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    /// <inheritdoc/>
    public void Configure(string? name, AzureQueueStorageTransportOptions options)
    {
        if (string.IsNullOrEmpty(name)) return;

        var configSection = configurationProvider.GetTransportConfiguration(name);
        if (configSection is null || !configSection.GetChildren().Any()) return;

        var serviceUrl = configSection.GetValue<Uri?>(nameof(AzureQueueStorageTransportCredentials.ServiceUrl))
                      ?? configSection.GetValue<Uri?>("Endpoint");
        options.Credentials = serviceUrl is not null
            ? new AzureQueueStorageTransportCredentials { ServiceUrl = serviceUrl }
            : (configSection.GetValue<string?>("ConnectionString") ?? options.Credentials);
    }

    /// <inheritdoc/>
    public void Configure(AzureQueueStorageTransportOptions options) => Configure(Options.Options.DefaultName, options);

    /// <inheritdoc/>
    public override void PostConfigure(string? name, AzureQueueStorageTransportOptions options)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));

        base.PostConfigure(name, options);

        // ensure we have a ServiceUrl when using AzureQueueStorageTransportCredentials
        if (options.Credentials.CurrentValue is AzureQueueStorageTransportCredentials asbtc && asbtc.ServiceUrl is null)
        {
            throw new InvalidOperationException($"'{nameof(AzureQueueStorageTransportCredentials.ServiceUrl)}' must be provided when using '{nameof(AzureQueueStorageTransportCredentials)}'.");
        }

        // Ensure there's only one consumer per event
        var registrations = busOptions.GetRegistrations(name);
        var multiple = registrations.FirstOrDefault(r => r.Consumers.Count > 1);
        if (multiple is not null)
        {
            throw new InvalidOperationException($"More than one consumer registered for '{multiple.EventType.Name}' yet "
                                               + "Azure Queue Storage does not support more than one consumer per event in the same application domain.");
        }

        // Ensure the entity names are not longer than the limits
        // See https://docs.microsoft.com/en-us/rest/api/storageservices/naming-queues-and-metadata#queue-names
        foreach (var reg in registrations)
        {
            // Set the IdFormat
            options.SetEventIdFormat(reg, busOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Queue);

            // Event names become topic names and they should not be longer than 63 characters
            if (reg.EventName!.Length > 63)
            {
                throw new InvalidOperationException($"EventName '{reg.EventName}' generated from '{reg.EventType.Name}' is too long. "
                                                   + "Azure Queue Storage does not allow more than 63 characters for Queue names.");
            }
        }
    }
}
