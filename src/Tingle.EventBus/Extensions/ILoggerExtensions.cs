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
                formatString: "Stopping bus.");

        private static readonly Action<ILogger, int, Exception> _startingTransport
            = LoggerMessage.Define<int>(
                eventId: new EventId(3, nameof(StartingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Starting transport. Consumers: '{Count}'");

        private static readonly Action<ILogger, Exception> _stoppingTransport
            = LoggerMessage.Define(
                eventId: new EventId(4, nameof(StoppingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping transport.");

        public static void StartingBus(this ILogger logger) => _startingBus(logger, null);

        public static void StoppingBus(this ILogger logger) => _stoppingBus(logger, null);

        public static void StartingTransport(this ILogger logger, int count) => _startingTransport(logger, count, null);

        public static void StoppingTransport(this ILogger logger) => _stoppingTransport(logger, null);
    }
}
