using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System;
using Tingle.EventBus;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure Service Bus based event bus.
    /// </summary>
    public class AzureServiceBusTransportOptions : EventBusTransportOptionsBase
    {
        /// <inheritdoc/>
        public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Topic;

        /// <summary>
        /// The connection string to Azure Service Bus.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The type of transport to use.
        /// Defaults to <see cref="ServiceBusTransportType.AmqpTcp"/>.
        /// </summary>
        public ServiceBusTransportType TransportType { get; set; } = ServiceBusTransportType.AmqpTcp;

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

        /// <summary>
        /// A function to create the processor options instead of using the default options.
        /// Some options set may still be overriding for proper operation of the the transport and the bus.
        /// </summary>
        public Func<ServiceBusProcessorOptions> CreateProcessorOptions { get; set; }
    }
}
