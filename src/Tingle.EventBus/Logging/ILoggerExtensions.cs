using System;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions on <see cref="ILogger"/> for the EventBus
    /// </summary>
    internal static class ILoggerExtensions
    {
        private static readonly Action<ILogger, Exception> _startingBus
            = LoggerMessage.Define(
                eventId: new EventId(1, nameof(StartingBus)),
                logLevel: LogLevel.Debug,
                formatString: "Starting bus.");

        private static readonly Action<ILogger, Exception> _stoppingBus
            = LoggerMessage.Define(
                eventId: new EventId(2, nameof(StoppingBus)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping bus receivers.");

        private static readonly Action<ILogger, int, Exception> _startingReceivers
            = LoggerMessage.Define<int>(
                eventId: new EventId(3, nameof(StartingBusReceivers)),
                logLevel: LogLevel.Debug,
                formatString: "Starting bus receivers. Consumers: '{Count}'");

        private static readonly Action<ILogger, Exception> _stoppingReceivers
            = LoggerMessage.Define(
                eventId: new EventId(4, nameof(StoppingBusReceivers)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping bus receivers.");

        public static void StartingBus(this ILogger logger) => _startingBus(logger, null);

        public static void StoppingBus(this ILogger logger) => _stoppingBus(logger, null);

        public static void StartingBusReceivers(this ILogger logger, int count) => _startingReceivers(logger, count, null);

        public static void StoppingBusReceivers(this ILogger logger) => _stoppingReceivers(logger, null);
    }
}
