﻿using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Tingle.EventBus;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Information, "Opening processor for EventHub: {EventHubName}\r\nConsumerGroup: {ConsumerGroup}\r\nPartitionId: {PartitionId}\r\nDefaultStartingPosition: {Position}")]
    public static partial void OpeningProcessor(this ILogger logger, string eventHubName, string consumerGroup, string partitionId, EventPosition position);

    [LoggerMessage(101, LogLevel.Information, "Closing processor for EventHub: {EventHubName}\r\nConsumerGroup: {ConsumerGroup}\r\nPartitionId: {PartitionId} (Reason: {Reason})")]
    public static partial void ClosingProcessor(this ILogger logger, string eventHubName, string consumerGroup, string partitionId, ProcessingStoppedReason reason);

    [LoggerMessage(102, LogLevel.Error, "Event processing faulted. Operation: {Operation}\r\nEventHub: {EventHubName}\r\nConsumerGroup: {ConsumerGroup}\r\nPartitionId: {PartitionId}")]
    public static partial void ProcessingError(this ILogger logger, string operation, string eventHubName, string consumerGroup, string partitionId, Exception ex);

    [LoggerMessage(103, LogLevel.Debug, "Stopping processor: {Processor}.")]
    public static partial void StoppingProcessor(this ILogger logger, string processor);

    [LoggerMessage(104, LogLevel.Debug, "Stopped processor for {Processor}.")]
    public static partial void StoppedProcessor(this ILogger logger, string processor);

    [LoggerMessage(105, LogLevel.Warning, "Stop processor faulted for {Processor}.")]
    public static partial void StopProcessorFaulted(this ILogger logger, string processor, Exception ex);


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

    [LoggerMessage(204, LogLevel.Debug, "Checkpointing {Partition} of '{EventHubName}/{ConsumerGroup}' at {SequenceNumber}.")]
    public static partial void Checkpointing(this ILogger logger, PartitionContext partition, string eventHubName, string consumerGroup, long sequenceNumber);


    [LoggerMessage(300, LogLevel.Debug, "Processor received event on EventHub: {EventHubName}\r\nConsumerGroup: {ConsumerGroup}\r\nPartitionId: {PartitionId}")]
    public static partial void ProcessorReceivedEvent(this ILogger logger, string eventHubName, string consumerGroup, string partitionId);

    [LoggerMessage(301, LogLevel.Debug, "Processing '{MessageId}' from '{EventHubName}/{ConsumerGroup}'.\r\nPartitionKey: {PartitionKey}\r\nSequenceNumber: {SequenceNumber}'")]
    public static partial void ProcessingEvent(this ILogger logger, string messageId, string eventHubName, string consumerGroup, string partitionKey, long sequenceNumber);

    [LoggerMessage(302, LogLevel.Information, "Received event: '{EventBusId}' from '{EventHubName}/{ConsumerGroup}'.\r\nPartitionKey: {PartitionKey}\r\nSequenceNumber: {SequenceNumber}'")]
    public static partial void ReceivedEvent(this ILogger logger, string? eventBusId, string eventHubName, string consumerGroup, string partitionKey, long sequenceNumber);
}
