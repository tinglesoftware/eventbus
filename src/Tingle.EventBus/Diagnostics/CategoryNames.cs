namespace Tingle.EventBus.Diagnostics
{
    /// <summary>
    /// The category names to use for logging and diagnostics
    /// </summary>
    internal static class CategoryNames
    {
        public const string EventBus = "EventBus";
        public const string Transports = EventBus + ".Transports";
        public const string Serializers = EventBus + ".Serializers";
    }
}
