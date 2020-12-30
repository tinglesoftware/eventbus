namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extenions on <see cref="ILoggerFactory"/>
    /// </summary>
    public static class ILoggerFactoryExtensions
    {
        /// <summary>
        /// Create a logger for a transport.
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="transportName"></param>
        /// <returns></returns>
        public static ILogger CreateTransportLogger(this ILoggerFactory factory, string transportName)
        {
            var category = string.Join(".", "EventBus", transportName);
            return factory.CreateLogger(category);
        }
    }
}
