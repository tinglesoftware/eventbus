namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// The preferred format to use for values generated for<see cref="EventContext.Id"/>.
    /// </summary>
    public enum EventIdFormat
    {
        /// <summary>
        /// Value is generated via <see cref="System.Guid.NewGuid"/>.
        /// For example <c>17a2a99b-9b40-4f93-9333-7036154d20a8</c>
        /// </summary>
        Guid,

        /// <summary>
        /// Value is generated via <see cref="System.Guid.NewGuid"/> but the dashes are removed.
        /// For example <c>17a2a99b9b404f9393337036154d20a8</c>
        /// </summary>
        GuidNoDashes,

        /// <summary>
        /// Value is generated via <see cref="System.Guid.NewGuid"/> but only the first 8 bytes are changed to <see cref="ulong"/>.
        /// For example <c>5734097450149521819</c>
        /// </summary>
        Long,

        /// <summary>
        /// Value is generated via <see cref="System.Guid.NewGuid"/> but only the first 8 bytes are changed to <see cref="ulong"/>.
        /// For example <c>4f939b4017a2a99b</c>
        /// </summary>
        LongHex,

        /// <summary>
        /// Value is generated via <see cref="System.Guid.NewGuid"/> and converted to two <see cref="ulong"/> concatenated.
        /// For example <c>573409745014952181912114767751129609107</c>
        /// </summary>
        DoubleLong,

        /// <summary>
        /// Value is generated via <see cref="System.Guid.NewGuid"/> and converted to two <see cref="ulong"/> concatenated.
        /// For example <c>4f939b4017a2a99ba8204d1536703393</c>
        /// </summary>
        DoubleLongHex,

        /// <summary>
        /// Value is generated via <see cref="System.Guid.NewGuid"/> and the bytes converted to base 64.
        /// For example <c>m6miF0Cbk0+TM3A2FU0gqA==</c>
        /// </summary>
        Random,
    }
}
