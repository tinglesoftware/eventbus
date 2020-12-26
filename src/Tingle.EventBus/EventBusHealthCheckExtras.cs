using System.Collections.Generic;

namespace Tingle.EventBus
{
    /// <summary>
    /// The extras from a health check on the event bus.
    /// </summary>
    public class EventBusHealthCheckExtras
    {
        /// <summary>
        ///  A human-readable description of the status the bus. (Optional).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Additional key-value pairs describing the health of the bus. (Optional).
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }
}
