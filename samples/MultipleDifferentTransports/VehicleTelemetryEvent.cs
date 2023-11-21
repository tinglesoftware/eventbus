﻿using System.Text.Json.Serialization;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace MultipleDifferentTransports;

internal record VehicleTelemetryEvent : IotHubEvent { }

internal class VehicleTelemetry
{
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
