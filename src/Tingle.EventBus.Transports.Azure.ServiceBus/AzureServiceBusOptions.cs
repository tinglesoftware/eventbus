using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure Service Bus based event bus.
    /// </summary>
    public class AzureServiceBusOptions
    {
        /// <summary>
        /// The connection string to Azure Service Bus.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets value indicating if the Service Bus namespace is in the Basic tier.
        /// The Basic tier does not support topics. In this case, the transport would make use of queues only.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool UseBasicTier { get; set; } = false;

        /// <summary>
        /// The type of transport to use.
        /// Defaults to <see cref="ServiceBusTransportType.AmqpTcp"/>.
        /// </summary>
        public ServiceBusTransportType TransportType { get; set; } = ServiceBusTransportType.AmqpTcp;

        /// <summary>
        /// Gets or sets value indicating if the Service Bus entities (e.g. queues, topics and subscriptions) should be created.
        /// If <see langword="false"/>, it is the responsibility of the caller to create entities.
        /// Always set this value to <see langword="false"/> when the <see cref="ConnectionString"/>
        /// is a shared access signature without the <c>Manage</c> permissions.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool EnableEntityCreation { get; set; } = true;

        /// <summary>
        /// A setup function for setting up options for a queue.
        /// This is only called before creation.
        /// </summary>
        public Action<CreateQueueOptions> SetupQueueOptions { get; set; }

        /// <summary>
        /// A setup function for setting up options for a topic.
        /// This is only called before creation.
        /// </summary>
        public Action<CreateTopicOptions> SetupTopicOptions { get; set; }

        /// <summary>
        /// A setup function for setting up options for a subscription.
        /// This is only called before creation.
        /// </summary>
        public Action<CreateSubscriptionOptions> SetupSubscriptionOptions { get; set; }
    }
}
