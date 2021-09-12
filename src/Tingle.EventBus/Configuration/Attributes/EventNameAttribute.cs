using System;

namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// Specify the EventName used for this event contract/type, overriding the generated one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class EventNameAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventName">The event name to use for the event.</param>
        public EventNameAttribute(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException($"'{nameof(eventName)}' cannot be null or whitespace", nameof(eventName));
            }

            EventName = eventName;
        }

        /// <summary>
        /// The name of the event mapped
        /// </summary>
        public string EventName { get; }
    }
}
