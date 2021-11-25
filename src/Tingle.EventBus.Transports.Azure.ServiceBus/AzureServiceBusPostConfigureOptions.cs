using Microsoft.Extensions.Options;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureServiceBusTransportOptions"/>.
/// </summary>
internal class AzureServiceBusPostConfigureOptions : AzureTransportPostConfigureOptions<AzureServiceBusTransportCredentials, AzureServiceBusTransportOptions>
{
    private readonly EventBusOptions busOptions;

    public AzureServiceBusPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
    {
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    /// <inheritdoc/>
    public override void PostConfigure(string name, AzureServiceBusTransportOptions options)
    {
        base.PostConfigure(name, options);

        // ensure we have a FullyQualifiedNamespace when using AzureServiceBusTransportCredentials
        if (options.Credentials!.Value is AzureServiceBusTransportCredentials asbtc && asbtc.FullyQualifiedNamespace is null)
        {
            throw new InvalidOperationException($"'{nameof(AzureServiceBusTransportCredentials.FullyQualifiedNamespace)}' must be provided when using '{nameof(AzureServiceBusTransportCredentials)}'.");
        }

        // Ensure the entity names are not longer than the limits
        // See https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas#messaging-quotas
        var registrations = busOptions.GetRegistrations(TransportNames.AzureServiceBus);
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
