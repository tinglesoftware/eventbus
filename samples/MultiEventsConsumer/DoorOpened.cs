namespace MultiEventsConsumer;

public class DoorOpened
{
    /// <summary>
    /// The vehicle who's door was opened.
    /// </summary>
    public string? VehicleId { get; set; }

    /// <summary>
    /// The kind of door that was opened.
    /// </summary>
    public DoorKind Kind { get; set; }

    /// <summary>
    /// When the door was opened.
    /// </summary>
    public DateTimeOffset Opened { get; set; }
}
