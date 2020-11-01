namespace Tingle.EventBus.Abstractions.Serialization
{
    /// <summary>
    /// Default configuration options for an implementation of <see cref="IEventSerializer"/>
    /// </summary>
    public class EventSerializerOptions
    {
        /// <summary>
        /// Gets or sets if the serializer should ignore null properties
        /// Defaults to <see langword="true" />
        /// </summary>
        public bool IgnoreNullValues { get; set; } = true;

        /// <summary>
        /// Gets or sets if the serializer should use indentation.
        /// </summary>
        public bool Indented { get; set; } = false;
    }
}
