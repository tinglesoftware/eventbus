using System.Text.Json.Serialization;

namespace AzureManagedIdentity;

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
