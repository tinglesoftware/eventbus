using System.Text.Json.Serialization;

namespace ConfigSample;

internal class VehicleDoorOpenedEvent
{
    public string? VehicleId { get; set; }
    public VehicleDoorKind Kind { get; set; }
    public DateTimeOffset? Opened { get; set; }
    public DateTimeOffset? Closed { get; set; }
}

internal class VehicleTelemetryEvent
{
    public string? DeviceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Action { get; set; }
    public VehicleDoorKind? VehicleDoorKind { get; set; }
    public VehicleDoorStatus? VehicleDoorStatus { get; set; }
    [JsonExtensionData]
    public Dictionary<string, object>? Extras { get; set; }
}

internal enum VehicleDoorStatus { Unknown, Open, Closed, }
internal enum VehicleDoorKind { FrontLeft, FrontRight, RearLeft, ReadRight, Hood, Trunk, }
