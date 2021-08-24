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
        /// The duration of time for which to delay delivery of a message after dequeuing.
        /// <see langword="null"/> does not apply any delay.
        /// Default value is <see langword="null"/>.
        /// </summary>
        public TimeSpan? DeliveryDelay { get; set; }
    }
}
