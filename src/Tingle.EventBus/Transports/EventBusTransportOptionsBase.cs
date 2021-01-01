using System;

namespace Tingle.EventBus.Transports
{
    /// <summary>
    /// The base options for an event bus transport
    /// </summary>
    public abstract class EventBusTransportOptionsBase
    {
        /// <summary>
        /// The delay to introduce everytime zero messages are received.
        /// This eases on the CPU consumption and reduces the query costs.
        /// This value must be between 30 seconds and 10 minutes.
        /// Defaults to 1 minute.
        /// </summary>
        public TimeSpan EmptyResultsDelay { get; set; } = TimeSpan.FromMinutes(1);
    }
}
