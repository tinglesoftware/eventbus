using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public class AzureServiceBusOptions
    {
        /// <summary>
        /// The connection string to Azure Service Bus.
        /// When not configured, <see cref="ConnectionStringBuilder"/> must be provided.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The connection options to Azure Service Bus.
        /// When not set, <see cref="ConnectionString"/> is used to create it.
        /// </summary>
        public ServiceBusConnectionStringBuilder ConnectionStringBuilder { get; set; }

        /// <summary>
        /// The type of transport to use.
        /// Defaults to <see cref="TransportType.Amqp"/>
        /// </summary>
        public TransportType TransportType { get; set; } = TransportType.Amqp;

        /// <summary>
        /// A setup function for setting up settings for a topic.
        /// This is only called before creation.
        /// </summary>
        public Action<TopicDescription> SetupTopicDescription { get; set; }

        /// <summary>
        /// A setup function for setting up settings for a subscription.
        /// This is only called before creation.
        /// </summary>
        public Action<SubscriptionDescription> SetupSubscriptionDescription { get; set; }
    }
}
