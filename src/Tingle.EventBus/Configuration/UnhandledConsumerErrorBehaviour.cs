namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// The behaviour to follow when an unhandled error in a consumer's
    /// <see cref="IEventConsumer{T}.ConsumeAsync(EventContext{T}, System.Threading.CancellationToken)"/>
    /// invocation results in an exception that is not handled.
    /// </summary>
    public enum UnhandledConsumerErrorBehaviour
    {
        /// <summary>
        /// Move the event to dead-letter entity.
        /// Handling of deadletter is transport specific.
        /// </summary>
        Deadletter,

        /// <summary>
        /// Discard the event.
        /// Depending on the transport, the event can be ignored, abandoned or skipped.
        /// </summary>
        Discard,
    }
}
