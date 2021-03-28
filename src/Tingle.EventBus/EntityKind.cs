namespace Tingle.EventBus
{
    /// <summary>
    /// The preferred entity type for events.
    /// </summary>
    public enum EntityKind
    {
        ///
        Broadcast,

        ///
        Queue,

        ///
        Stream,
    }
}
