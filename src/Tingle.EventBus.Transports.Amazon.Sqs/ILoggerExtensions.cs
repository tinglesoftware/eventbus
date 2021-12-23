namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Warning, "Amazon SNS does not support delay or scheduled publish. Use SQS instead by setting the entity type to queue.")]
    public static partial void SchedulingNotSupportedBySns(this ILogger logger);

    [LoggerMessage(101, LogLevel.Warning, "Amazon SNS does not support batching. The events will be looped through one by one.")]
    public static partial void BatchingNotSupported(this ILogger logger);


    [LoggerMessage(200, LogLevel.Information, "Sending {EventId} to '{TopicArn}'. Scheduled: {Scheduled}.")]
    public static partial void SendingToTopic(this ILogger logger, string? eventId, string topicArn, DateTimeOffset? scheduled);

    [LoggerMessage(201, LogLevel.Information, "Sending {EventId} to '{QueueUrl}'. Scheduled: {Scheduled}.")]
    public static partial void SendingToQueue(this ILogger logger, string? eventId, string queueUrl, DateTimeOffset? scheduled);

    [LoggerMessage(201, LogLevel.Warning, "Delay for {EventId} to '{QueueUrl}' capped at 15min. Scheduled: {Scheduled}.")]
    public static partial void DelayCapped(this ILogger logger, string? eventId, string queueUrl, DateTimeOffset? scheduled);


    [LoggerMessage(300, LogLevel.Trace, "No messages on '{QueueUrl}', delaying check for {Delay}.")]
    public static partial void NoMessages(this ILogger logger, string queueUrl, TimeSpan delay);

    [LoggerMessage(301, LogLevel.Debug, "Received {MessagesCount} messages on '{QueueUrl}'")]
    public static partial void ReceivedMessages(this ILogger logger, int messagesCount, string queueUrl);

    [LoggerMessage(302, LogLevel.Information, "Received message: '{MessageId}' containing Event '{EventId}' from '{QueueUrl}'")]
    public static partial void ReceivedMessage(this ILogger logger, string messageId, string? eventId, string queueUrl);

    [LoggerMessage(303, LogLevel.Debug, "Processing '{MessageId}' from '{QueueUrl}'")]
    public static partial void ProcessingMessage(this ILogger logger, string messageId, string queueUrl);

    [LoggerMessage(304, LogLevel.Trace, "Deleting '{MessageId}' from '{QueueUrl}'")]
    public static partial void DeletingMessage(this ILogger logger, string messageId, string queueUrl);
}
