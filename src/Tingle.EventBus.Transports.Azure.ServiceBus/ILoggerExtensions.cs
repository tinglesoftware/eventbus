using Azure.Messaging.ServiceBus;
using Tingle.EventBus;
using Tingle.EventBus.Transports.Azure.ServiceBus;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Information, "Starting processing on {EntityPath}.")]
    public static partial void StartingProcessing(this ILogger logger, string entityPath);

    [LoggerMessage(101, LogLevel.Debug, "Stopping client: {Processor}.")]
    public static partial void StoppingProcessor(this ILogger logger, string processor);

    [LoggerMessage(102, LogLevel.Debug, "Stopped processor for {Processor}.")]
    public static partial void StoppedProcessor(this ILogger logger, string processor);

    [LoggerMessage(103, LogLevel.Warning, "Stop processor faulted for {Processor}.")]
    public static partial void StopProcessorFaulted(this ILogger logger, string processor, Exception ex);

    [LoggerMessage(104, LogLevel.Debug, "Creating processor for queue '{QueueName}'.")]
    public static partial void CreatingQueueProcessor(this ILogger logger, string queueName);

    [LoggerMessage(105, LogLevel.Debug, "Creating processor for topic '{TopicName}' and subscription '{SubscriptionName}'.")]
    public static partial void CreatingSubscriptionProcessor(this ILogger logger, string topicName, string subscriptionName);

    [LoggerMessage(106, LogLevel.Debug, "Creating sender for queue '{QueueName}'.")]
    public static partial void CreatingQueueSender(this ILogger logger, string queueName);

    [LoggerMessage(107, LogLevel.Debug, "Creating sender for topic '{TopicName}'.")]
    public static partial void CreatingTopicSender(this ILogger logger, string topicName);

    [LoggerMessage(108, LogLevel.Trace, "Entity creation is disabled. Queue creation skipped.")]
    public static partial void QueueEntityCreationDisabled(this ILogger logger);

    [LoggerMessage(109, LogLevel.Debug, "Checking if queue '{QueueName}' exists.")]
    public static partial void CheckingQueueExistence(this ILogger logger, string queueName);

    [LoggerMessage(110, LogLevel.Trace, "Queue '{QueueName}' does not exist, preparing creation.")]
    public static partial void CreatingQueuePreparation(this ILogger logger, string queueName);

    [LoggerMessage(111, LogLevel.Information, "Creating queue '{QueueName}'")]
    public static partial void CreatingQueue(this ILogger logger, string queueName);

    [LoggerMessage(112, LogLevel.Debug, "Checking if topic '{TopicName}' exists.")]
    public static partial void CheckingTopicExistence(this ILogger logger, string topicName);

    [LoggerMessage(113, LogLevel.Trace, "Entity creation is disabled. Topic creation skipped.")]
    public static partial void TopicEntityCreationDisabled(this ILogger logger);

    [LoggerMessage(114, LogLevel.Trace, "Topic '{TopicName}' does not exist, preparing creation.")]
    public static partial void CreatingTopicPreparation(this ILogger logger, string topicName);

    [LoggerMessage(115, LogLevel.Information, "Creating topic '{TopicName}'")]
    public static partial void CreatingTopic(this ILogger logger, string topicName);

    [LoggerMessage(116, LogLevel.Trace, "Entity creation is disabled. Subscription creation skipped.")]
    public static partial void SubscriptionEntityCreationDisabled(this ILogger logger);

    [LoggerMessage(117, LogLevel.Debug, "Checking if subscription '{SubscriptionName}' under topic '{TopicName}' exists")]
    public static partial void CheckingSubscriptionExistence(this ILogger logger, string subscriptionName, string topicName);

    [LoggerMessage(118, LogLevel.Trace, "Subscription '{SubscriptionName}' under topic '{TopicName}' does not exist, preparing creation.")]
    public static partial void CreatingSubscriptionPreparation(this ILogger logger, string subscriptionName, string topicName);

    [LoggerMessage(119, LogLevel.Information, "Creating subscription '{SubscriptionName}' under topic '{TopicName}'")]
    public static partial void CreatingSubscription(this ILogger logger, string subscriptionName, string topicName);



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

    [LoggerMessage(302, LogLevel.Debug, "Post Consume action: {Action} for message: {MessageId} from '{EntityPath}' containing Event: {EventId}.")]
    public static partial void PostConsumeAction(this ILogger logger, PostConsumeAction? action, string? messageId, string entityPath, string? eventId);

    [LoggerMessage(303, LogLevel.Debug, "Message receiving faulted. Namespace: {FullyQualifiedNamespace}, Entity Path: {EntityPath}, Source: {ErrorSource}")]
    public static partial void MessageReceivingFaulted(this ILogger logger, string fullyQualifiedNamespace, string entityPath, ServiceBusErrorSource errorSource, Exception ex);
}
