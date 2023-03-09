using Confluent.Kafka;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{

    [LoggerMessage(200, LogLevel.Warning, "Kafka does not support delay or scheduled publish.")]
    public static partial void SchedulingNotSupported(this ILogger logger);

    [LoggerMessage(201, LogLevel.Warning, "Kafka does not support expiring events.")]
    public static partial void ExpiryNotSupported(this ILogger logger);

    [LoggerMessage(202, LogLevel.Warning, "Kafka does not support batching. The events will be looped through one by one.")]
    public static partial void BatchingNotSupported(this ILogger logger);


    [LoggerMessage(300, LogLevel.Information, "Consumer received data at {Offset}")]
    public static partial void ConsumerReceivedData(this ILogger logger, TopicPartitionOffset offset);

    [LoggerMessage(301, LogLevel.Trace, "Reached end of topic {Topic}, Partition: {Partition}, Offset: {Offset}.")]
    public static partial void EndOfTopic(this ILogger logger, string topic, Partition partition, Offset offset);

    [LoggerMessage(302, LogLevel.Debug, "Processing '{MessageKey}' from '{Topic}', Partition: '{Partition}'. Offset: '{Offset}'")]
    public static partial void ProcessingMessage(this ILogger logger, string messageKey, string topic, int partition, long offset);

    [LoggerMessage(303, LogLevel.Information, "Received event: '{EventBusId}' from '{Topic}', Partition: '{Partition}'. Offset: '{Offset}'")]
    public static partial void ReceivedEvent(this ILogger logger, string? eventBusId, string topic, int partition, long offset);

    [LoggerMessage(304, LogLevel.Debug, "Checkpointing '{Topic}', Partition: '{Partition}'. Offset: '{Offset}'")]
    public static partial void Checkpointing(this ILogger logger, string topic, int partition, long offset);
}
