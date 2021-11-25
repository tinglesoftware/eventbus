using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Serialization;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static class ILoggerExtensions
{
    #region Bus (100 series)

    private static readonly Action<ILogger, TimeSpan, Exception?> _delayedBusStartup
        = LoggerMessage.Define<TimeSpan>(
            eventId: new EventId(101, nameof(DelayedBusStartup)),
            logLevel: LogLevel.Information,
            formatString: "Delaying bus startup for '{StartupDelay}'.");

    private static readonly Action<ILogger, Exception?> _delayedBusStartupError
        = LoggerMessage.Define(
            eventId: new EventId(102, nameof(DelayedBusStartupError)),
            logLevel: LogLevel.Error,
            formatString: "Starting bus delayed error.");

    private static readonly Action<ILogger, Exception?> _startupReadinessCheck
        = LoggerMessage.Define(
            eventId: new EventId(103, nameof(StartupReadinessCheck)),
            logLevel: LogLevel.Information,
            formatString: "Performing readiness check before starting bus.");

    private static readonly Action<ILogger, Exception?> _startupReadinessCheckFailed
        = LoggerMessage.Define(
            eventId: new EventId(104, nameof(StartupReadinessCheckFailed)),
            logLevel: LogLevel.Error,
            formatString: "Startup readiness check failed or timedout.");

    private static readonly Action<ILogger, int, Exception?> _startingBus
        = LoggerMessage.Define<int>(
            eventId: new EventId(105, nameof(StartingBus)),
            logLevel: LogLevel.Debug,
            formatString: "Starting bus with {TransportsCount} transports.");

    private static readonly Action<ILogger, Exception?> _stoppingBus
        = LoggerMessage.Define(
            eventId: new EventId(106, nameof(StoppingBus)),
            logLevel: LogLevel.Debug,
            formatString: "Stopping bus.");

    public static void DelayedBusStartup(this ILogger logger, TimeSpan delay) => _delayedBusStartup(logger, delay, null);
    public static void DelayedBusStartupError(this ILogger logger, Exception ex) => _delayedBusStartupError(logger, ex);
    public static void StartupReadinessCheck(this ILogger logger) => _startupReadinessCheck(logger, null);
    public static void StartupReadinessCheckFailed(this ILogger logger, Exception ex) => _startupReadinessCheckFailed(logger, ex);
    public static void StartingBus(this ILogger logger, int count) => _startingBus(logger, count, null);
    public static void StoppingBus(this ILogger logger) => _stoppingBus(logger, null);

    #endregion

    #region Transports (200 series)

    private static readonly Action<ILogger, int, TimeSpan, Exception?> _startingTransport
        = LoggerMessage.Define<int, TimeSpan>(
            eventId: new EventId(201, nameof(StartingTransport)),
            logLevel: LogLevel.Debug,
            formatString: "Starting transport. Consumers: {ConsumersCount}, EmptyResultsDelay: '{EmptyResultsDelay}'");

    private static readonly Action<ILogger, Exception?> _stoppingTransport
        = LoggerMessage.Define(
            eventId: new EventId(202, nameof(StoppingTransport)),
            logLevel: LogLevel.Debug,
            formatString: "Stopping transport.");

    public static void StartingTransport(this ILogger logger, int count, TimeSpan emptyResultsDelay)
        => _startingTransport(logger, count, emptyResultsDelay, null);

    public static void StoppingTransport(this ILogger logger) => _stoppingTransport(logger, null);

    #endregion

    #region Events (300 series)

    private static readonly Action<ILogger, string?, string, Exception?> _sendingEvent
        = LoggerMessage.Define<string?, string>(
            eventId: new EventId(301, nameof(SendingEvent)),
            logLevel: LogLevel.Information,
            formatString: "Sending event '{EventId}' using '{TransportName}' transport");

    private static readonly Action<ILogger, string?, string, DateTimeOffset, string, TimeSpan, Exception?> _sendingEventWithScheduled
        = LoggerMessage.Define<string?, string, DateTimeOffset, string, TimeSpan>(
            eventId: new EventId(301, nameof(SendingEvent)),
            logLevel: LogLevel.Information,
            formatString: "Sending event '{EventId}' using '{TransportName}' transport. Scheduled: {Scheduled:o}, Delay: {ReadableDelay} ({RetryDelay})");

    private static readonly Action<ILogger, int, string, IList<string?>, Exception?> _sendingEvents
        = LoggerMessage.Define<int, string, IList<string?>>(
            eventId: new EventId(302, nameof(SendingEvents)),
            logLevel: LogLevel.Information,
            formatString: "Sending {EventsCount} events using '{TransportName}' transport.\r\nEvents: {EventIds}");

    private static readonly Action<ILogger, int, string, DateTimeOffset, string, TimeSpan, IList<string?>, Exception?> _sendingEventsWithScheduled
        = LoggerMessage.Define<int, string, DateTimeOffset, string, TimeSpan, IList<string?>>(
            eventId: new EventId(302, nameof(SendingEvents)),
            logLevel: LogLevel.Information,
            formatString: "Sending {EventsCount} events using '{TransportName}' transport. Scheduled: {Scheduled:o}, Delay: {ReadableDelay} ({RetryDelay}).\r\nEvents: {EventIds}");

    private static readonly Action<ILogger, string, string, Exception?> _cancelingEvent
        = LoggerMessage.Define<string, string>(
            eventId: new EventId(303, nameof(CancelingEvent)),
            logLevel: LogLevel.Information,
            formatString: "Canceling event '{EventId}' on '{TransportName}' transport");

    private static readonly Action<ILogger, int, string, IList<string>, Exception?> _cancelingEvents
        = LoggerMessage.Define<int, string, IList<string>>(
            eventId: new EventId(303, nameof(CancelingEvents)),
            logLevel: LogLevel.Information,
            formatString: "Canceling {EventsCount} events on '{TransportName}' transport.\r\nEvents: {EventIds}");

    private static readonly Action<ILogger, string?, Exception?> _consumeFailedTransportHandling
        = LoggerMessage.Define<string?>(
            eventId: new EventId(304, nameof(ConsumeFailed)),
            logLevel: LogLevel.Error,
            formatString: "Event processing failed. Transport specific handling in play. (EventId:{EventId})");

    private static readonly Action<ILogger, string?, Exception?> _consumeFailedDeadletter
        = LoggerMessage.Define<string?>(
            eventId: new EventId(304, nameof(ConsumeFailed)),
            logLevel: LogLevel.Error,
            formatString: "Event processing failed. Moving to deadletter. (EventId:{EventId})");

    private static readonly Action<ILogger, string?, Exception?> _consumeFailedDiscard
        = LoggerMessage.Define<string?>(
            eventId: new EventId(304, nameof(ConsumeFailed)),
            logLevel: LogLevel.Error,
            formatString: "Event processing failed. Discarding event. (EventId:{EventId})");

    public static void SendingEvent(this ILogger logger, string? eventId, string transportName, DateTimeOffset? scheduled = null)
    {
        if (scheduled == null)
        {
            _sendingEvent(logger, eventId, transportName, null);
        }
        else
        {
            var (readableDelay, delay) = GetDelay(scheduled.Value);
            _sendingEventWithScheduled(logger, eventId, transportName, scheduled.Value, readableDelay, delay, null);
        }
    }

    public static void SendingEvent(this ILogger logger, EventContext @event, string transportName, DateTimeOffset? scheduled = null)
    {
        SendingEvent(logger, @event.Id, transportName, scheduled);
    }

    public static void SendingEvents(this ILogger logger, IList<string?> eventIds, string transportName, DateTimeOffset? scheduled = null)
    {
        if (scheduled == null)
        {
            _sendingEvents(logger, eventIds.Count, transportName, eventIds, null);
        }
        else
        {
            var (readableDelay, delay) = GetDelay(scheduled.Value);
            _sendingEventsWithScheduled(logger, eventIds.Count, transportName, scheduled.Value, readableDelay, delay, eventIds, null);
        }
    }

    public static void SendingEvents(this ILogger logger, IList<EventContext> events, string transportName, DateTimeOffset? scheduled = null)
    {
        SendingEvents(logger, events.Select(e => e.Id).ToList(), transportName, scheduled);
    }

    public static void SendingEvents<T>(this ILogger logger, IList<EventContext<T>> events, string transportName, DateTimeOffset? scheduled = null)
        where T : class
    {
        SendingEvents(logger, events.Select(e => e.Id).ToList(), transportName, scheduled);
    }

    public static void CancelingEvent(this ILogger logger, string eventId, string transportName)
    {
        _cancelingEvent(logger, eventId, transportName, null);
    }

    public static void CancelingEvents(this ILogger logger, IList<string> eventIds, string transportName)
    {
        _cancelingEvents(logger, eventIds.Count, transportName, eventIds, null);
    }

    public static void ConsumeFailed(this ILogger logger, string? eventId, UnhandledConsumerErrorBehaviour? behaviour, Exception ex)
    {
        Action<ILogger, string?, Exception> action = behaviour switch
        {
            UnhandledConsumerErrorBehaviour.Deadletter => _consumeFailedDeadletter,
            UnhandledConsumerErrorBehaviour.Discard => _consumeFailedDiscard,
            _ => _consumeFailedTransportHandling,
        };

        action?.Invoke(logger, eventId, ex);
    }

    private static (string readableDelay, TimeSpan delay) GetDelay(DateTimeOffset scheduled)
    {
        var delay = scheduled - DateTimeOffset.UtcNow;
        return (delay.ToReadableString(), delay);
    }

    #endregion

    #region Readiness (400 series)

    private static readonly Action<ILogger, TimeSpan, Exception?> _readinessCheck
        = LoggerMessage.Define<TimeSpan>(
            eventId: new EventId(401, nameof(ReadinessCheck)),
            logLevel: LogLevel.Information,
            formatString: "Performing readiness check. Timeout: '{ReadinessTimeout}'.");

    private static readonly Action<ILogger, TimeSpan, Exception?> _readinessCheckTimedout
        = LoggerMessage.Define<TimeSpan>(
            eventId: new EventId(402, nameof(StartupReadinessCheckFailed)),
            logLevel: LogLevel.Error,
            formatString: "Startup readiness check failed or timedout after '{ReadinessTimeout}'.");

    private static readonly Action<ILogger, Exception?> _readinessCheckDisabled
        = LoggerMessage.Define(
            eventId: new EventId(401, nameof(ReadinessCheckDisabled)),
            logLevel: LogLevel.Debug,
            formatString: "Readiness check is disabled. Assumes ready by default.");

    public static void ReadinessCheck(this ILogger logger, TimeSpan timeout) => _readinessCheck(logger, timeout, null);

    public static void ReadinessCheckTimedout(this ILogger logger, TimeSpan timeout) => _readinessCheckTimedout(logger, timeout, null);

    public static void ReadinessCheckDisabled(this ILogger logger) => _readinessCheckDisabled(logger, null);

    #endregion

    #region Serialization (500 series)

    private static readonly Action<ILogger, string?, string, Exception?> _deserializationResultedInNull
        = LoggerMessage.Define<string?, string>(
            eventId: new EventId(501, nameof(DeserializationResultedInNull)),
            logLevel: LogLevel.Warning,
            formatString: "Deserialization resulted in a null which should not happen. Identifier: '{Identifier}', Type: '{EventType}'.");

    private static readonly Action<ILogger, string?, string?, string, Exception?> _deserializedEventShouldNotBeNull
        = LoggerMessage.Define<string?, string?, string>(
            eventId: new EventId(502, nameof(DeserializedEventShouldNotBeNull)),
            logLevel: LogLevel.Warning,
            formatString: "Deserialized event should not have a null event. Identifier: '{Identifier}', EventId: '{EventId}', Type: '{EventType}'.");

    public static void DeserializationResultedInNull(this ILogger logger, string? id, string eventType)
    {
        _deserializationResultedInNull(logger, id, eventType, null);
    }

    public static void DeserializationResultedInNull(this ILogger logger, DeserializationContext context)
    {
        logger.DeserializationResultedInNull(id: context.Identifier, eventType: context.Registration.EventType.FullName!);
    }

    public static void DeserializedEventShouldNotBeNull(this ILogger logger, string? id, string? eventId, string eventType)
    {
        _deserializedEventShouldNotBeNull(logger, id, eventId, eventType, null);
    }

    public static void DeserializedEventShouldNotBeNull(this ILogger logger, DeserializationContext context, string? eventId)
    {
        logger.DeserializedEventShouldNotBeNull(id: context.Identifier,
                                                eventId: eventId,
                                                eventType: context.Registration.EventType.FullName!);
    }

    #endregion
}
