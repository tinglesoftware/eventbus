﻿using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Options for configuring Azure Service Bus based event bus.
/// </summary>
public class AzureServiceBusTransportOptions : AzureTransportOptions<AzureServiceBusTransportCredentials>
{
    /// <inheritdoc/>
    public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Broadcast;

    /// <summary>
    /// The type of transport to use.
    /// Defaults to <see cref="ServiceBusTransportType.AmqpTcp"/>.
    /// </summary>
    public ServiceBusTransportType TransportType { get; set; } = ServiceBusTransportType.AmqpTcp;

    #region Defaults

    /// <summary>
    /// The default lock duration applies on entities created.
    /// Defaults to is 5 minutes.
    /// </summary>
    public TimeSpan DefaultLockDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The default TTL(Time To Live) to set on entities created.
    /// Defaults to is 366 days.
    /// </summary>
    public TimeSpan DefaultMessageTimeToLive { get; set; } = TimeSpan.FromDays(366);

    /// <summary>
    /// The default maximum delivery count to set on entities created.
    /// Defaults to is 5.
    /// </summary>
    public int DefaultMaxDeliveryCount { get; set; } = 5;

    /// <summary>
    /// The default auto delete on idle time to set on entities created.
    /// Defaults to is 427 days.
    /// </summary>
    public TimeSpan DefaultAutoDeleteOnIdle { get; set; } = TimeSpan.FromDays(427);

    /// <summary>
    /// The default maximum auto lock renewal duration to set when creating processors.
    /// This value must be more than the <c>LockDuration</c> set on the entity.
    /// It can also be overridden per event/consumer using <see cref="ServiceBusProcessorOptions"/>
    /// passed in through <see cref="SetupProcessorOptions"/>.
    /// </summary>
    public TimeSpan DefaultMaxAutoLockRenewDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The default number of messages that will be eagerly requested from Queues or Subscriptions
    /// and queued locally, intended to help maximize throughput by allowing the processor/receiver
    /// to receive from a local cache rather than waiting on a service request.
    /// <br/>
    /// Defaults to is 0.
    /// <br/>
    /// It can also be overridden per event/consumer using <see cref="ServiceBusProcessorOptions"/>
    /// passed in through <see cref="SetupProcessorOptions"/>.
    /// </summary>
    public int DefaultPrefetchCount { get; set; } = 0;

    #endregion

    /// <summary>
    /// A function for setting up options for a queue.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, CreateQueueOptions>? SetupQueueOptions { get; set; }

    /// <summary>
    /// A function for setting up options for a topic.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, CreateTopicOptions>? SetupTopicOptions { get; set; }

    /// <summary>
    /// A function for setting up options for a subscription.
    /// This is only called before creation.
    /// </summary>
    public Action<EventConsumerRegistration, CreateSubscriptionOptions>? SetupSubscriptionOptions { get; set; }

    /// <summary>
    /// A function for setting up options for a processor.
    /// This is only called before creation.
    /// </summary>
    public Action<EventRegistration, EventConsumerRegistration, ServiceBusProcessorOptions>? SetupProcessorOptions { get; set; }
}
