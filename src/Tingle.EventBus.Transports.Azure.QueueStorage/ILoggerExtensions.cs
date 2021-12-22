namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Information, "Ensuring queue '{QueueName}' exists.")]
    public static partial void EnsuringQueue(this ILogger logger, string queueName);

    [LoggerMessage(101, LogLevel.Trace, "Entity creation is disabled. Queue creation skipped.")]
    public static partial void EntityCreationDisabled(this ILogger logger);


    [LoggerMessage(200, LogLevel.Warning, "Azure Queue Storage does not support batching. The events will be looped through one by one.")]
    public static partial void BatchingNotSupported(this ILogger logger);

    [LoggerMessage(201, LogLevel.Information, "Sending {EventId} to '{QueueName}'. Scheduled: {Scheduled}.")]
    public static partial void SendingMessage(this ILogger logger, string? eventId, string queueName, DateTimeOffset? scheduled);

    [LoggerMessage(202, LogLevel.Information, "Cancelling '{MessageId}|{PopReceipt}' on '{QueueName}'.")]
    public static partial void CancelingMessage(this ILogger logger, string messageId, string popReceipt, string queueName);


    [LoggerMessage(300, LogLevel.Trace, "No messages on '{QueueName}', delaying check for {Delay}.")]
    public static partial void NoMessages(this ILogger logger, string queueName, TimeSpan delay);

    [LoggerMessage(301, LogLevel.Debug, "Received {MessagesCount} messages on '{QueueName}'")]
    public static partial void ReceivedMessages(this ILogger logger, int messagesCount, string queueName);

    [LoggerMessage(302, LogLevel.Debug, "Processing '{MessageId}' from '{QueueName}'")]
    public static partial void ProcessingMessage(this ILogger logger, string messageId, string queueName);

    [LoggerMessage(303, LogLevel.Information, "Received message: '{MessageId}' containing Event '{EventId}' from '{QueueName}'")]
    public static partial void ReceivedMessage(this ILogger logger, string messageId, string? eventId, string queueName);

    [LoggerMessage(304, LogLevel.Trace, "Deleting '{MessageId}' on '{QueueName}'")]
    public static partial void DeletingMessage(this ILogger logger, string messageId, string queueName);
}
