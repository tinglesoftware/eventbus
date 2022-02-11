using Tingle.EventBus;
using Tingle.EventBus.Transports.InMemory.Client;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Information, "Starting processing on {EntityPath}")]
    public static partial void StartingProcessing(this ILogger logger, string entityPath);

    [LoggerMessage(101, LogLevel.Debug, "Stopping client: {Processor}")]
    public static partial void StoppingProcessor(this ILogger logger, string processor);

    [LoggerMessage(102, LogLevel.Debug, "Stopped processor for {Processor}")]
    public static partial void StoppedProcessor(this ILogger logger, string processor);

    [LoggerMessage(103, LogLevel.Warning, "Stop processor faulted for {Processor}")]
    public static partial void StopProcessorFaulted(this ILogger logger, string processor, Exception ex);

    [LoggerMessage(104, LogLevel.Debug, "Creating processor for queue '{QueueName}'")]
    public static partial void CreatingQueueProcessor(this ILogger logger, string queueName);

    [LoggerMessage(105, LogLevel.Debug, "Creating processor for topic '{TopicName}' and subscription '{SubscriptionName}'")]
    public static partial void CreatingSubscriptionProcessor(this ILogger logger, string topicName, string subscriptionName);

    [LoggerMessage(200, LogLevel.Warning, "InMemory EventBus uses a short-lived timer that is not persisted for scheduled publish.")]
    public static partial void SchedulingShortLived(this ILogger logger);

    [LoggerMessage(201, LogLevel.Information, "Sending {EventId} to '{EntityPath}'. Scheduled: {Scheduled}")]
    public static partial void SendingMessage(this ILogger logger, string? eventId, string entityPath, DateTimeOffset? scheduled);

    [LoggerMessage(202, LogLevel.Information, "Sending {EventsCount} messages to '{EntityPath}'. Scheduled: {Scheduled}. Events:\r\n- {EventIds}")]
    private static partial void SendingMessages(this ILogger logger, int eventsCount, string entityPath, DateTimeOffset? scheduled, string eventIds);

    public static void SendingMessages(this ILogger logger, IList<string?> eventIds, string entityPath, DateTimeOffset? scheduled)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.SendingMessages(eventsCount: eventIds.Count,
                               entityPath: entityPath,
                               scheduled: scheduled,
                               eventIds: string.Join("\r\n- ", eventIds));
    }

    public static void SendingMessages<T>(this ILogger logger, IList<EventContext<T>> events, string entityPath, DateTimeOffset? scheduled = null)
        where T : class
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.SendingMessages(events.Select(e => e.Id).ToList(), entityPath, scheduled);
    }

    [LoggerMessage(203, LogLevel.Information, "Canceling scheduled message: {SequenceNumber} on {EntityPath}")]
    public static partial void CancelingMessage(this ILogger logger, long sequenceNumber, string entityPath);

    [LoggerMessage(204, LogLevel.Information, "Canceling {messagesCount} scheduled messages on {EntityPath}:\r\n- {SequenceNumbers}")]
    private static partial void CancelingMessages(this ILogger logger, int messagesCount, string entityPath, string sequenceNumbers);

    public static void CancelingMessages(this ILogger logger, IList<long> sequenceNumbers, string entityPath)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.CancelingMessages(messagesCount: sequenceNumbers.Count,
                                 entityPath: entityPath,
                                 sequenceNumbers: string.Join("\r\n- ", sequenceNumbers));
    }


    [LoggerMessage(300, LogLevel.Information, "Received message: '{SequenceNumber}' containing Event '{EventId}' from '{EntityPath}'")]
    public static partial void ReceivedMessage(this ILogger logger, long sequenceNumber, string? eventId, string entityPath);

    [LoggerMessage(301, LogLevel.Debug, "Processing '{MessageId}' from '{EntityPath}'")]
    public static partial void ProcessingMessage(this ILogger logger, string? messageId, string entityPath);

    [LoggerMessage(303, LogLevel.Debug, "Message receiving faulted. Entity Path: {EntityPath}, Source: {ErrorSource}")]
    public static partial void MessageReceivingFaulted(this ILogger logger, string entityPath, InMemoryErrorSource errorSource, Exception ex);
}
