namespace Tingle.EventBus
{
    /// <summary>
    /// The source used when generating names for consumers.
    /// </summary>
    public enum ConsumerNameSource
    {
        /// <summary>
        /// The type name of the consumer is used.
        /// </summary>
        TypeName,

        /// <summary>
        /// The name of the hosting application is used.
        /// </summary>
        ApplicationName,
    }
}
