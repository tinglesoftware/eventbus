namespace Tingle.EventBus.Abstractions.Serialization
{
    public class EventBusSerializerOptions
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
