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

    [LoggerMessage(102, LogLevel.Debug, "Starting bus with {TransportsCount} transports.")]
    public static partial void StartingBus(this ILogger logger, int transportsCount);

    [LoggerMessage(103, LogLevel.Debug, "Stopping bus.")]
    public static partial void StoppingBus(this ILogger logger);

    [LoggerMessage(104, LogLevel.Debug, "Stopping transports.")]
    public static partial void StoppingTransports(this ILogger logger);

    #endregion

    #region Transports (200 series)

    [LoggerMessage(201, LogLevel.Debug, "Starting transport '{Name}'. Consumers: {ConsumersCount}, EmptyResultsDelay: '{EmptyResultsDelay}'")]
    public static partial void StartingTransport(this ILogger logger, string name, int consumersCount, TimeSpan emptyResultsDelay);

    [LoggerMessage(202, LogLevel.Debug, "Stopping transport '{Name}'.")]
    public static partial void StoppingTransport(this ILogger logger, string name);

    #endregion

    #region Events (300 series)

    [LoggerMessage(301, LogLevel.Information, "Sending event '{EventBusId}' using '{TransportName}' transport.")]
    private static partial void SendingEvent(this ILogger logger, string? eventBusId, string transportName);

    [LoggerMessage(302, LogLevel.Information, "Sending event '{EventBusId}' using '{TransportName}' transport. Scheduled: {Scheduled:o}")]
    private static partial void SendingScheduledEvent(this ILogger logger, string? eventBusId, string transportName, DateTimeOffset scheduled);

    public static void SendingEvent(this ILogger logger, string? eventBusId, string transportName, DateTimeOffset? scheduled)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        if (scheduled == null)
        {
            logger.SendingEvent(eventBusId, transportName);
        }
        else
        {
            logger.SendingScheduledEvent(eventBusId, transportName, scheduled.Value);
        }
    }

    [LoggerMessage(303, LogLevel.Information, "Sending {EventsCount} events using '{TransportName}' transport.\r\nEvents: {EventBusIds}")]
    private static partial void SendingEvents(this ILogger logger, int eventsCount, string transportName, IList<string?> eventBusIds);

    [LoggerMessage(304, LogLevel.Information, "Sending {EventsCount} events using '{TransportName}' transport. Scheduled: {Scheduled:o}.\r\nEvents: {EventBusIds}")]
    private static partial void SendingScheduledEvents(this ILogger logger, int eventsCount, string transportName, DateTimeOffset scheduled, IList<string?> eventBusIds);

    public static void SendingEvents(this ILogger logger, IList<string?> eventBusIds, string transportName, DateTimeOffset? scheduled = null)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        if (scheduled == null)
        {
            logger.SendingEvents(eventsCount: eventBusIds.Count, transportName: transportName, eventBusIds: eventBusIds);
        }
        else
        {
            logger.SendingScheduledEvents(eventBusIds.Count, transportName, scheduled.Value, eventBusIds);
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

    [LoggerMessage(307, LogLevel.Error, "Event processing failed on '{TransportName}'. {Action} (EventId: {EventBusId})")]
    public static partial void ConsumeFailed(this ILogger logger, string transportName, string action, string? eventBusId, Exception ex);

    public static void ConsumeFailed(this ILogger logger, string transportName, UnhandledConsumerErrorBehaviour? behaviour, string? eventBusId, Exception ex)
    {
        var action = behaviour switch
        {
            UnhandledConsumerErrorBehaviour.Deadletter => "Moving to dead-letter.",
            UnhandledConsumerErrorBehaviour.Discard => "Discarding event.",
            _ => "Transport specific handling in play.",
        };

        logger.ConsumeFailed(transportName, action, eventBusId, ex);
    }

    #endregion

    #region Serialization (400 series)

    [LoggerMessage(401, LogLevel.Warning, "Deserialization resulted in a null which should not happen. Identifier: '{Identifier}', Type: '{EventType}'.")]
    public static partial void DeserializationResultedInNull(this ILogger logger, string? identifier, string? eventType);

    [LoggerMessage(402, LogLevel.Warning, "Deserialized event should not have a null event. Identifier: '{Identifier}', EventId: '{EventBusId}', Type: '{EventType}'.")]
    public static partial void DeserializedEventShouldNotBeNull(this ILogger logger, string? identifier, string? eventBusId, string? eventType);

    #endregion
}
