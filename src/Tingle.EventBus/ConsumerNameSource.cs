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

        /// <summary>
        /// The name of the application
        /// and the type name of the consumer are combined.
        /// </summary>
        ApplicationAndTypeName,

        /// <summary>
        /// The prefix provided in the bus options.
        /// </summary>
        Prefix,

        /// <summary>
        /// The prefix provided in the bus options
        /// and the type name of the consumer are cmobined.
        /// </summary>
        PrefixAndTypeName,
    }
}
