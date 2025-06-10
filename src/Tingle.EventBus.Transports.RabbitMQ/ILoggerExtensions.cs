namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(200, LogLevel.Warning, "RabbitMQ does not support batching. The events will be looped through one by one.")]
    public static partial void BatchingNotSupported(this ILogger logger);
}
