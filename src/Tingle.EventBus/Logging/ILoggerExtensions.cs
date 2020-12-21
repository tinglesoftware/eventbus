using System;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions on <see cref="ILogger"/> for the EventBus
    /// </summary>
    internal static class ILoggerExtensions
    {
        private static readonly Action<ILogger, int, Exception> _startingBus
            = LoggerMessage.Define<int>(
                eventId: new EventId(1, nameof(StartingBus)),
                logLevel: LogLevel.Debug,
                formatString: "Starting bus receivers. Consumers: '{ConsumersCount}'");

        private static readonly Action<ILogger, Exception> _stoppingBus
            = LoggerMessage.Define(
                eventId: new EventId(2, nameof(StoppingBus)),
                logLevel: LogLevel.Debug,
                formatString: "Stopping bus receivers.");

        public static void StartingBus(this ILogger logger, int consumersCount) => _startingBus(logger, consumersCount, null);

        public static void StoppingBus(this ILogger logger) => _stoppingBus(logger, null);
    }
}
