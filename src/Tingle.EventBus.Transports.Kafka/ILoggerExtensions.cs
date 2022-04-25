using Confluent.Kafka;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Warning, "Kafka does not support delay or scheduled publish.")]
    public static partial void SchedulingNotSupported(this ILogger logger);

    [LoggerMessage(101, LogLevel.Warning, "Kafka does not support batching. The events will be looped through one by one.")]
    public static partial void BatchingNotSupported(this ILogger logger);

    [LoggerMessage(102, LogLevel.Information, "Consumer recevied data at {Offset}")]
    public static partial void ConsumerReceivedData(this ILogger logger, TopicPartitionOffset offset);

    [LoggerMessage(103, LogLevel.Trace, "Reached end of topic {Topic}, Partition: {Partition}, Offset: {Offset}.")]
    public static partial void EndOfTopic(this ILogger logger, string topic, Partition partition, Offset offset);

    [LoggerMessage(104, LogLevel.Debug, "Processing '{MessageKey}")]
    public static partial void ProcessingMessage(this ILogger logger, string messageKey);

    [LoggerMessage(105, LogLevel.Information, "Received event: '{MessageKey}' containing Event '{EventBusId}'")]
    public static partial void ReceivedEvent(this ILogger logger, string messageKey, string? eventBusId);
}
