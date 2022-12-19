using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureServiceBusTransportOptions"/>.
/// </summary>
internal class AzureServiceBusConfigureOptions : AzureTransportConfigureOptions<AzureServiceBusTransportCredentials, AzureServiceBusTransportOptions>,
                                                 IConfigureNamedOptions<AzureServiceBusTransportOptions>
{
    private readonly EventBusOptions busOptions;

    /// <summary>
    /// Initializes a new <see cref="AzureServiceBusConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public AzureServiceBusConfigureOptions(IEventBusConfigurationProvider configurationProvider, IOptions<EventBusOptions> busOptionsAccessor)
        : base(configurationProvider)
    {
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    /// <inheritdoc/>
    protected override void Configure(IConfiguration configuration, AzureServiceBusTransportOptions options)
    {
        base.Configure(configuration, options);

        options.TransportType = configuration.GetValue<ServiceBusTransportType?>(nameof(options.TransportType)) ?? options.TransportType;
        options.DefaultLockDuration = configuration.GetValue<TimeSpan?>(nameof(options.DefaultLockDuration)) ?? options.DefaultLockDuration;
        options.DefaultMessageTimeToLive = configuration.GetValue<TimeSpan?>(nameof(options.DefaultMessageTimeToLive)) ?? options.DefaultMessageTimeToLive;
        options.DefaultMaxDeliveryCount = configuration.GetValue<int?>(nameof(options.DefaultMaxDeliveryCount)) ?? options.DefaultMaxDeliveryCount;
        options.DefaultAutoDeleteOnIdle = configuration.GetValue<TimeSpan?>(nameof(options.DefaultAutoDeleteOnIdle)) ?? options.DefaultAutoDeleteOnIdle;
        options.DefaultMaxAutoLockRenewDuration = configuration.GetValue<TimeSpan?>(nameof(options.DefaultMaxAutoLockRenewDuration)) ?? options.DefaultMaxAutoLockRenewDuration;
        options.DefaultPrefetchCount = configuration.GetValue<int?>(nameof(options.DefaultPrefetchCount)) ?? options.DefaultPrefetchCount;

        var fullyQualifiedNamespace = configuration.GetValue<string?>(nameof(AzureServiceBusTransportCredentials.FullyQualifiedNamespace))
                                   ?? configuration.GetValue<string?>("Namespace");
        options.Credentials = fullyQualifiedNamespace is not null
            ? new AzureServiceBusTransportCredentials { FullyQualifiedNamespace = fullyQualifiedNamespace }
            : (configuration.GetValue<string?>("ConnectionString") ?? options.Credentials);
    }

    /// <inheritdoc/>
    public override void PostConfigure(string? name, AzureServiceBusTransportOptions options)
    {
        base.PostConfigure(name, options);

        // ensure we have a FullyQualifiedNamespace when using AzureServiceBusTransportCredentials
        if (options.Credentials.CurrentValue is AzureServiceBusTransportCredentials asbtc && asbtc.FullyQualifiedNamespace is null)
        {
            throw new InvalidOperationException($"'{nameof(AzureServiceBusTransportCredentials.FullyQualifiedNamespace)}' must be provided when using '{nameof(AzureServiceBusTransportCredentials)}'.");
        }

        // Ensure the entity names are not longer than the limits
        // See https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas#messaging-quotas
        var registrations = busOptions.GetRegistrations(name!);
        foreach (var reg in registrations)
        {
            // Set the IdFormat
            options.SetEventIdFormat(reg, busOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast, EntityKind.Queue);

            // Event names become Topic and Queue names and they should not be longer than 260 characters
            if (reg.EventName!.Length > 260)
            {
                throw new InvalidOperationException($"EventName '{reg.EventName}' generated from '{reg.EventType.Name}' is too long. "
                                                   + "Azure Service Bus does not allow more than 260 characters for Topic and Queue names.");
            }

            // Consumer names become Subscription names and they should not be longer than 50 characters
            // When not using Queues, ConsumerName -> SubscriptionName does not happen
            if (reg.EntityKind == EntityKind.Broadcast)
            {
                foreach (var ecr in reg.Consumers)
                {
                    if (ecr.ConsumerName!.Length > 50)
                    {
                        throw new InvalidOperationException($"ConsumerName '{ecr.ConsumerName}' generated from '{ecr.ConsumerType.Name}' is too long. "
                                                           + "Azure Service Bus does not allow more than 50 characters for Subscription names.");
                    }
                }
            }
        }
    }
}
