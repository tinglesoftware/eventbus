using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System;
using Tingle.EventBus;
using Tingle.EventBus.Registrations;

namespace Microsoft.Extensions.DependencyInjection
{
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
        /// It can also be overriden per event/consumer using <see cref="ServiceBusProcessorOptions"/>
        /// passed in through <see cref="SetupProcessorOptions"/>.
        /// </summary>
        public TimeSpan DefaultMaxAutoLockRenewDuration { get; set; } = TimeSpan.FromMinutes(10);

        #endregion

        /// <summary>
        /// A setup function for setting up options for a queue.
        /// This is only called before creation.
        /// </summary>
        public Action<EventRegistration, CreateQueueOptions> SetupQueueOptions { get; set; }

        /// <summary>
        /// A setup function for setting up options for a topic.
        /// This is only called before creation.
        /// </summary>
        public Action<EventRegistration, CreateTopicOptions> SetupTopicOptions { get; set; }

        /// <summary>
        /// A setup function for setting up options for a subscription.
        /// This is only called before creation.
        /// </summary>
        public Action<EventConsumerRegistration, CreateSubscriptionOptions> SetupSubscriptionOptions { get; set; }

        /// <summary>
        /// A function to create the processor options instead of using the default options.
        /// Some options set may still be overriding for proper operation of the the transport and the bus.
        /// </summary>
        public Action<EventRegistration, EventConsumerRegistration, ServiceBusProcessorOptions> SetupProcessorOptions { get; set; }
    }
}
