using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ConfigSample;

internal class VehicleTelemetryEvent
{
    public string? DeviceId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string? Action { get; set; }

    public VehicleDoorKind? VehicleDoorKind { get; set; }
    public VehicleDoorStatus? VehicleDoorStatus { get; set; }

    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}

public enum VehicleDoorStatus
{
    Unknown,
    Open,
    Closed,
}

public enum VehicleDoorKind
{
    FrontLeft,
    FrontRight,
    RearLeft,
    ReadRight,
    Hood,
    Trunk,
}
