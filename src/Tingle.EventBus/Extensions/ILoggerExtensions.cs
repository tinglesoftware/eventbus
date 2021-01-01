using System;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions on <see cref="ILogger"/> for the EventBus
    /// </summary>
    internal static class ILoggerExtensions
    {
        private static readonly Action<ILogger, int, Exception> _startingTransport
            = LoggerMessage.Define<int>(
                eventId: new EventId(1, nameof(StartingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Starting transport. Consumers: '{Count}'");

        private static readonly Action<ILogger, Exception> _stoppingTransport
            = LoggerMessage.Define(
                eventId: new EventId(2, nameof(StoppingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping transport.");

        public static void StartingTransport(this ILogger logger, int count) => _startingTransport(logger, count, null);

        public static void StoppingTransport(this ILogger logger) => _stoppingTransport(logger, null);
    }
}
