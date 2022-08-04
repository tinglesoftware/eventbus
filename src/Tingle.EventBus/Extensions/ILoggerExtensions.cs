using Tingle.EventBus;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    #region Bus (100 series)

    [LoggerMessage(101, LogLevel.Debug, "Application did not startup. Event Bus cannot continue.")]
    public static partial void ApplicationDidNotStartup(this ILogger logger);

    [LoggerMessage(102, LogLevel.Information, "Delaying bus startup for '{StartupDelay}'.")]
    public static partial void DelayedBusStartup(this ILogger logger, TimeSpan startupDelay);

    [LoggerMessage(103, LogLevel.Error, "Starting bus delayed error.")]
    public static partial void DelayedBusStartupError(this ILogger logger, Exception ex);

    [LoggerMessage(104, LogLevel.Information, "Performing readiness check before starting bus.")]
    public static partial void StartupReadinessCheck(this ILogger logger);

    [LoggerMessage(105, LogLevel.Error, "Startup readiness check failed or timedout.")]
    public static partial void StartupReadinessCheckFailed(this ILogger logger, Exception ex);

    [LoggerMessage(106, LogLevel.Debug, "Starting bus with {TransportsCount} transports.")]
    public static partial void StartingBus(this ILogger logger, int transportsCount);

    [LoggerMessage(107, LogLevel.Debug, "Stopping bus.")]
    public static partial void StoppingBus(this ILogger logger);

    #endregion

    #region Transports (200 series)

    [LoggerMessage(201, LogLevel.Debug, "Starting transport. Consumers: {ConsumersCount}, EmptyResultsDelay: '{EmptyResultsDelay}'")]
    public static partial void StartingTransport(this ILogger logger, int consumersCount, TimeSpan emptyResultsDelay);

    [LoggerMessage(202, LogLevel.Debug, "Stopping transport.")]
    public static partial void StoppingTransport(this ILogger logger);

    #endregion

    #region Events (300 series)

    [LoggerMessage(301, LogLevel.Information, "Sending event '{EventBusId}' using '{TransportName}' transport.")]
    private static partial void SendingEvent(this ILogger logger, string? eventBusId, string transportName);

    [LoggerMessage(302, LogLevel.Information, "Sending event '{EventBusId}' using '{TransportName}' transport. Scheduled: {Scheduled:o}, Delay: {ReadableDelay} ({RetryDelay})")]
    private static partial void SendingScheduledEvent(this ILogger logger, string? eventBusId, string transportName, DateTimeOffset scheduled, string readableDelay, TimeSpan retryDelay);

    public static void SendingEvent(this ILogger logger, string? eventBusId, string transportName, DateTimeOffset? scheduled)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        if (scheduled == null)
        {
            logger.SendingEvent(eventBusId, transportName);
        }
        else
        {
            var (readableDelay, delay) = GetDelay(scheduled.Value);
            logger.SendingScheduledEvent(eventBusId, transportName, scheduled.Value, readableDelay, delay);
        }
    }

    [LoggerMessage(303, LogLevel.Information, "Sending {EventsCount} events using '{TransportName}' transport.\r\nEvents: {EventBusIds}")]
    private static partial void SendingEvents(this ILogger logger, int eventsCount, string transportName, IList<string?> eventBusIds);

    [LoggerMessage(304, LogLevel.Information, "Sending {EventsCount} events using '{TransportName}' transport. Scheduled: {Scheduled:o}, Delay: {ReadableDelay} ({RetryDelay}).\r\nEvents: {EventBusIds}")]
    private static partial void SendingScheduledEvents(this ILogger logger, int eventsCount, string transportName, DateTimeOffset scheduled, string readableDelay, TimeSpan retryDelay, IList<string?> eventBusIds);

    public static void SendingEvents(this ILogger logger, IList<string?> eventBusIds, string transportName, DateTimeOffset? scheduled = null)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        if (scheduled == null)
        {
            logger.SendingEvents(eventsCount: eventBusIds.Count, transportName: transportName, eventBusIds: eventBusIds);
        }
        else
        {
            var (readableDelay, delay) = GetDelay(scheduled.Value);
            logger.SendingScheduledEvents(eventBusIds.Count, transportName, scheduled.Value, readableDelay, delay, eventBusIds);
        }
    }

    public static void SendingEvents<T>(this ILogger logger, IList<EventContext<T>> events, string transportName, DateTimeOffset? scheduled = null)
        where T : class
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.SendingEvents(events.Select(e => e.Id).ToList(), transportName, scheduled);
    }

    [LoggerMessage(305, LogLevel.Information, "Canceling event '{EventBusId}' on '{TransportName}' transport")]
    public static partial void CancelingEvent(this ILogger logger, string eventBusId, string transportName);

    [LoggerMessage(306, LogLevel.Information, "Canceling {EventsCount} events on '{TransportName}' transport.\r\nEvents: {EventBusIds}")]
    public static partial void CancelingEvents(this ILogger logger, int eventsCount, IList<string> eventBusIds, string transportName);
    public static void CancelingEvents(this ILogger logger, IList<string> eventBusIds, string transportName) => logger.CancelingEvents(eventBusIds.Count, eventBusIds, transportName);

    [LoggerMessage(307, LogLevel.Error, "Event processing failed. {Action} (EventId: {EventBusId})")]
    public static partial void ConsumeFailed(this ILogger logger, string action, string? eventBusId, Exception ex);

    public static void ConsumeFailed(this ILogger logger, UnhandledConsumerErrorBehaviour? behaviour, string? eventBusId, Exception ex)
    {
        var action = behaviour switch
        {
            UnhandledConsumerErrorBehaviour.Deadletter => "Moving to deadletter.",
            UnhandledConsumerErrorBehaviour.Discard => "Discarding event.",
            _ => "Transport specific handling in play.",
        };

        logger.ConsumeFailed(action, eventBusId, ex);
    }

    private static (string readableDelay, TimeSpan delay) GetDelay(DateTimeOffset scheduled)
    {
        var delay = scheduled - DateTimeOffset.UtcNow;
        return (delay.ToReadableString(), delay);
    }

    #endregion

    #region Readiness (400 series)

    [LoggerMessage(401, LogLevel.Information, "Performing readiness check. Timeout: '{ReadinessTimeout}'.")]
    public static partial void ReadinessCheck(this ILogger logger, TimeSpan readinessTimeout);

    [LoggerMessage(402, LogLevel.Error, "Startup readiness check failed or timedout after '{ReadinessTimeout}'.")]
    public static partial void ReadinessCheckTimedout(this ILogger logger, TimeSpan readinessTimeout);

    [LoggerMessage(403, LogLevel.Debug, "Readiness check is disabled. Assumes ready by default.")]
    public static partial void ReadinessCheckDisabled(this ILogger logger);

    #endregion

    #region Serialization (500 series)

    [LoggerMessage(501, LogLevel.Warning, "Deserialization resulted in a null which should not happen. Identifier: '{Identifier}', Type: '{EventType}'.")]
    public static partial void DeserializationResultedInNull(this ILogger logger, string? identifier, string? eventType);

    [LoggerMessage(502, LogLevel.Warning, "Deserialized event should not have a null event. Identifier: '{Identifier}', EventId: '{EventBusId}', Type: '{EventType}'.")]
    public static partial void DeserializedEventShouldNotBeNull(this ILogger logger, string? identifier, string? eventBusId, string? eventType);

    #endregion
}
