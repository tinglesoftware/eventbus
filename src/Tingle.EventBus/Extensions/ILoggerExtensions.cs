using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions on <see cref="ILogger"/> for the EventBus
    /// </summary>
    internal static class ILoggerExtensions
    {
        #region Bus (100 series)

        private static readonly Action<ILogger, TimeSpan, Exception> _delayedBusStartup
            = LoggerMessage.Define<TimeSpan>(
                eventId: new EventId(101, nameof(DelayedBusStartup)),
                logLevel: LogLevel.Information,
                formatString: "Delaying bus startup for '{StartupDelay}'.");

        private static readonly Action<ILogger, Exception> _delayedBusStartupError
            = LoggerMessage.Define(
                eventId: new EventId(102, nameof(DelayedBusStartupError)),
                logLevel: LogLevel.Error,
                formatString: "Starting bus delayed error.");

        private static readonly Action<ILogger, int, Exception> _startingBus
            = LoggerMessage.Define<int>(
                eventId: new EventId(103, nameof(StartingBus)),
                logLevel: LogLevel.Debug,
                formatString: "Starting bus with {TransportsCount} transports.");

        private static readonly Action<ILogger, Exception> _stoppingBus
            = LoggerMessage.Define(
                eventId: new EventId(104, nameof(StoppingBus)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping bus.");

        public static void DelayedBusStartup(this ILogger logger, TimeSpan delay) => _delayedBusStartup(logger, delay, null);
        public static void DelayedBusStartupError(this ILogger logger, Exception ex) => _delayedBusStartupError(logger, ex);
        public static void StartingBus(this ILogger logger, int count) => _startingBus(logger, count, null);
        public static void StoppingBus(this ILogger logger) => _stoppingBus(logger, null);

        #endregion

        #region Transports (200 series)

        private static readonly Action<ILogger, int, TimeSpan, Exception> _startingTransport
            = LoggerMessage.Define<int, TimeSpan>(
                eventId: new EventId(201, nameof(StartingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Starting transport. Consumers: {ConsumersCount}, EmptyResultsDelay: '{EmptyResultsDelay}'");

        private static readonly Action<ILogger, Exception> _stoppingTransport
            = LoggerMessage.Define(
                eventId: new EventId(202, nameof(StoppingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping transport.");

        public static void StartingTransport(this ILogger logger, int count, TimeSpan emptyResultsDelay)
            => _startingTransport(logger, count, emptyResultsDelay, null);

        public static void StoppingTransport(this ILogger logger) => _stoppingTransport(logger, null);

        #endregion

        #region Events (300 series)

        private static readonly Action<ILogger, string, string, Exception> _sendingEvent
            = LoggerMessage.Define<string, string>(
                eventId: new EventId(301, nameof(SendingEvent)),
                logLevel: LogLevel.Information,
                formatString: "Sending event '{EventId}' using '{TransportName}' transport");

        private static readonly Action<ILogger, string, string, DateTimeOffset, string, TimeSpan, Exception> _sendingEventWithScheduled
            = LoggerMessage.Define<string, string, DateTimeOffset, string, TimeSpan>(
                eventId: new EventId(301, nameof(SendingEvent)),
                logLevel: LogLevel.Information,
                formatString: "Sending event '{EventId}' using '{TransportName}' transport. Scheduled: {Scheduled:o}, Delay: {ReadableDelay} ({RetryDelay})");

        private static readonly Action<ILogger, int, string, IList<string>, Exception> _sendingEvents
            = LoggerMessage.Define<int, string, IList<string>>(
                eventId: new EventId(302, nameof(SendingEvents)),
                logLevel: LogLevel.Information,
                formatString: "Sending {EventsCount} events using '{TransportName}' transport.\r\nEvents: {EventIds}");

        private static readonly Action<ILogger, int, string, DateTimeOffset, string, TimeSpan, IList<string>, Exception> _sendingEventsWithScheduled
            = LoggerMessage.Define<int, string, DateTimeOffset, string, TimeSpan, IList<string>>(
                eventId: new EventId(302, nameof(SendingEvents)),
                logLevel: LogLevel.Information,
                formatString: "Sending {EventsCount} events using '{TransportName}' transport. Scheduled: {Scheduled:o}, Delay: {ReadableDelay} ({RetryDelay}).\r\nEvents: {EventIds}");

        private static readonly Action<ILogger, string, string, Exception> _cancelingEvent
            = LoggerMessage.Define<string, string>(
                eventId: new EventId(303, nameof(CancelingEvent)),
                logLevel: LogLevel.Information,
                formatString: "Canceling event '{EventId}' on '{TransportName}' transport");

        private static readonly Action<ILogger, int, string, IList<string>, Exception> _cancelingEvents
            = LoggerMessage.Define<int, string, IList<string>>(
                eventId: new EventId(303, nameof(CancelingEvents)),
                logLevel: LogLevel.Information,
                formatString: "Canceling {EventsCount} events on '{TransportName}' transport.\r\nEvents: {EventIds}");

        public static void SendingEvent(this ILogger logger, string eventId, string transportName, DateTimeOffset? scheduled = null)
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

        public static void SendingEvents(this ILogger logger, IList<string> eventIds, string transportName, DateTimeOffset? scheduled = null)
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

        private static (string readableDelay, TimeSpan delay) GetDelay(DateTimeOffset scheduled)
        {
            var delay = scheduled - DateTimeOffset.UtcNow;
            return (delay.ToReadableString(), delay);
        }

        #endregion
    }
}
