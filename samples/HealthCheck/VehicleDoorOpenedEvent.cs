namespace HealthCheck;

public class VehicleDoorOpenedEvent
{
    public string? VehicleId { get; set; }
    public VehicleDoorKind Kind { get; set; }
    public DateTimeOffset? Opened { get; set; }
    public DateTimeOffset? Closed { get; set; }
}
