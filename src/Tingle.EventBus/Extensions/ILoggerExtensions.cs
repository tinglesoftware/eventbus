using System;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions on <see cref="ILogger"/> for the EventBus
    /// </summary>
    internal static class ILoggerExtensions
    {
        private static readonly Action<ILogger, int, TimeSpan, Exception> _startingTransport
            = LoggerMessage.Define<int, TimeSpan>(
                eventId: new EventId(1, nameof(StartingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Starting transport. Consumers: {Count}, EmptyResultsDelay: '{EmptyResultsDelay}'");

        private static readonly Action<ILogger, Exception> _stoppingTransport
            = LoggerMessage.Define(
                eventId: new EventId(2, nameof(StoppingTransport)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping transport.");

        public static void StartingTransport(this ILogger logger, int count, TimeSpan emptyResultsDelay)
        {
            _startingTransport(logger, count, emptyResultsDelay, null);
        }

        public static void StoppingTransport(this ILogger logger) => _stoppingTransport(logger, null);
    }
}
