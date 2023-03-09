using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Tingle.EventBus;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Information, "Opening processor for '{EventHubName}/{ConsumerGroup}', PartitionId: {PartitionId}. DefaultStartingPosition: {Position}")]
    public static partial void OpeningProcessor(this ILogger logger, string eventHubName, string consumerGroup, string partitionId, EventPosition position);

    [LoggerMessage(101, LogLevel.Information, "Closing processor for '{EventHubName}/{ConsumerGroup}', PartitionId: {PartitionId}. Reason: {Reason}")]
    public static partial void ClosingProcessor(this ILogger logger, string eventHubName, string consumerGroup, string partitionId, ProcessingStoppedReason reason);

    [LoggerMessage(102, LogLevel.Error, "Event processing faulted for '{EventHubName}/{ConsumerGroup}', PartitionId: {PartitionId}. Operation: {Operation}")]
    public static partial void ProcessingError(this ILogger logger, string eventHubName, string consumerGroup, string partitionId, string operation, Exception ex);

    [LoggerMessage(103, LogLevel.Debug, "Stopping processor: {Processor}.")]
    public static partial void StoppingProcessor(this ILogger logger, string processor);

    [LoggerMessage(104, LogLevel.Debug, "Stopped processor for {Processor}.")]
    public static partial void StoppedProcessor(this ILogger logger, string processor);

    [LoggerMessage(105, LogLevel.Warning, "Stop processor faulted for {Processor}.")]
    public static partial void StopProcessorFaulted(this ILogger logger, string processor, Exception ex);

    [LoggerMessage(106, LogLevel.Information, "Checkpointing event processing at '{BlobContainerUri}'")]
    public static partial void CheckpointStore(this ILogger logger, Uri blobContainerUri);


    [LoggerMessage(200, LogLevel.Warning, "Azure EventHubs does not support delay or scheduled publish.")]
    public static partial void SchedulingNotSupported(this ILogger logger);

    [LoggerMessage(201, LogLevel.Warning, "Azure EventHubs does not support expiring events.")]
    public static partial void ExpiryNotSupported(this ILogger logger);

    [LoggerMessage(202, LogLevel.Information, "Sending {EventBusId} to '{EventHubName}'. Scheduled: {Scheduled}")]
    public static partial void SendingEvent(this ILogger logger, string? eventBusId, string eventHubName, DateTimeOffset? scheduled);

    [LoggerMessage(203, LogLevel.Information, "Sending {EventsCount} events to '{EventHubName}'. Scheduled: {Scheduled}. Events:\r\n- {EventBusIds}")]
    private static partial void SendingEvents(this ILogger logger, int eventsCount, string eventHubName, DateTimeOffset? scheduled, string eventBusIds);

    public static void SendingEvents(this ILogger logger, IList<string?> eventBusIds, string eventHubName, DateTimeOffset? scheduled)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.SendingEvents(eventsCount: eventBusIds.Count,
                             eventHubName: eventHubName,
                             scheduled: scheduled,
                             eventBusIds: string.Join("\r\n- ", eventBusIds));
    }

    public static void SendingEvents<T>(this ILogger logger, IList<EventContext<T>> events, string eventHubName, DateTimeOffset? scheduled = null)
        where T : class
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.SendingEvents(events.Select(e => e.Id).ToList(), eventHubName, scheduled);
    }


    [LoggerMessage(300, LogLevel.Debug, "Processor received event from '{EventHubName}/{ConsumerGroup}', PartitionId: '{PartitionId}'")]
    public static partial void ProcessorReceivedEvent(this ILogger logger, string eventHubName, string consumerGroup, string partitionId);

    [LoggerMessage(301, LogLevel.Debug, "Processing '{MessageId}' from '{EventHubName}/{ConsumerGroup}', PartitionId: '{PartitionId}'. PartitionKey: '{PartitionKey}' SequenceNumber: '{SequenceNumber}'")]
    public static partial void ProcessingEvent(this ILogger logger, string messageId, string eventHubName, string consumerGroup, string partitionId, string? partitionKey, long sequenceNumber);

    [LoggerMessage(302, LogLevel.Information, "Received event: '{EventBusId}' from '{EventHubName}/{ConsumerGroup}', PartitionId: '{PartitionId}'. PartitionKey: '{PartitionKey}' SequenceNumber: '{SequenceNumber}'")]
    public static partial void ReceivedEvent(this ILogger logger, string? eventBusId, string eventHubName, string consumerGroup, string partitionId, string? partitionKey, long sequenceNumber);

    [LoggerMessage(304, LogLevel.Debug, "Checkpointing '{EventHubName}/{ConsumerGroup}', PartitionId: '{PartitionId}' at '{SequenceNumber}'")]
    public static partial void Checkpointing(this ILogger logger, string eventHubName, string consumerGroup, string partitionId, long sequenceNumber);
}
