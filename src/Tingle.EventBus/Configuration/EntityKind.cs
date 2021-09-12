namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// The preferred entity type for events.
    /// </summary>
    public enum EntityKind
    {
        /// <summary>
        /// Represents an entity that broadcasts to multiple destinations.
        /// Depending on the transport, this may be referred to as a topic, exchange, stream or hub
        /// </summary>
        Broadcast,

        /// <summary>
        /// Represents an entity that only maps the event to only one destination (itself).
        /// Depending on the transport, it may have a different name.
        /// </summary>
        Queue,
    }
}
