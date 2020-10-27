namespace Tingle.EventBus.Abstractions
{
    public class EventContext
    {
        /// <summary>
        /// The headers published alongside the event.
        /// </summary>
        public EventHeaders Headers { get; set; }
    }

    public class EventContext<T> : EventContext
    {
        /// <summary>
        /// The event published
        /// </summary>
        public T Event { get; set; }
    }
}
