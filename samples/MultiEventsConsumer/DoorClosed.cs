using System;

namespace MultiEventsConsumer
{
    public class DoorClosed
    {
        /// <summary>
        /// The vehicle who's door was closed.
        /// </summary>
        public string? VehicleId { get; set; }

        /// <summary>
        /// The kind of door that was opened.
        /// </summary>
        public DoorKind Kind { get; set; }

        /// <summary>
        /// When the door was opened.
        /// </summary>
        public DateTimeOffset Closed { get; set; }
    }
}
