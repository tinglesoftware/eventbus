namespace Tingle.EventBus.Transport
{
    /// <summary>
    /// The names of known transports
    /// </summary>
    public static class TransportNames
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public const string AmazonKinesis = "amazon-kinesis";
        public const string AmazonSqs = "amazon-sqs";
        public const string AzureEventHubs = "azure-event-hubs";
        public const string AzureQueueStorage = "azure-queue-storage";
        public const string AzureServiceBus = "azure-service-bus";
        public const string InMemory = "inmemory";
        public const string Kafka = "kafka";
        public const string RabbitMq = "rabbitmq";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
