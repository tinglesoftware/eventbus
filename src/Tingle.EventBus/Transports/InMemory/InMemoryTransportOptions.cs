using System;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring the in-memory based event bus.
    /// </summary>
    public class InMemoryTransportOptions : EventBusTransportOptionsBase
    {
        /// <summary>
        /// The durationof time for which to delay delivery of a message after dequeuing.
        /// Default value is 1 second.
        /// </summary>
        public TimeSpan DeliveryDelay { get; set; } = TimeSpan.FromSeconds(1);
    }
}
