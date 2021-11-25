namespace Tingle.EventBus;

/// <summary>
/// The names of known transports
/// </summary>
public static class TransportNames
{
    /// <see href="https://aws.amazon.com/kinesis/"/>
    public const string AmazonKinesis = "amazon-kinesis";

    /// <see href="https://aws.amazon.com/sqs/"/>
    public const string AmazonSqs = "amazon-sqs";

    /// <see href="https://azure.microsoft.com/en-us/services/event-hubs/"/>
    public const string AzureEventHubs = "azure-event-hubs";

    /// <see href="https://azure.microsoft.com/en-us/services/storage/queues/"/>
    public const string AzureQueueStorage = "azure-queue-storage";

    /// <see href="https://azure.microsoft.com/en-us/services/service-bus/"/>
    public const string AzureServiceBus = "azure-service-bus";

    /// 
    public const string InMemory = "inmemory";

    /// <see href="https://kafka.apache.org/"/>
    public const string Kafka = "kafka";

    /// <see href="https://www.rabbitmq.com/"/>
    public const string RabbitMq = "rabbitmq";
}
