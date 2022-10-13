using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>
/// Details about an operational event from Azure IoT Hub.
/// </summary>
public sealed record IotHubOperationalEvent<T>
{
    /// <summary>
    /// Creates an instance of <see cref="IotHubOperationalEvent{T}"/>.
    /// </summary>
    /// <param name="hubName">The name of the hub where the event happened.</param>
    /// <param name="deviceId">Unique identifier of the device.</param>
    /// <param name="moduleId">Unique identifier of the module within the device.</param>
    /// <param name="type">Type of operational event.</param>
    /// <param name="operationTimestamp">Time when the operation was done.</param>
    /// <param name="event">The actual event.</param>
    public IotHubOperationalEvent(string? hubName, string? deviceId, string? moduleId, IotHubOperationalEventType type, string? operationTimestamp, T @event)
    {
        HubName = hubName;
        DeviceId = deviceId;
        ModuleId = moduleId;
        Type = type;
        OperationTimestamp = operationTimestamp;
        Event = @event;
    }

    /// <summary>The name of the hub where the event happened.</summary>
    public string? HubName { get; }

    /// <summary>Unique identifier of the device.</summary>
    public string? DeviceId { get; }

    /// <summary>Unique identifier of the module within the device.</summary>
    public string? ModuleId { get; }

    /// <summary>Type of operational event.</summary>
    public IotHubOperationalEventType Type { get; }

    /// <summary>Time when the operation was done.</summary>
    public string? OperationTimestamp { get; }

    /// <summary>The actual event.</summary>
    public T Event { get; }
}

/// <summary>
/// Basics of a device lifecycle event.
/// </summary>
public record IotHubDeviceLifecycleEvent: AbstractIotHubEvent
{
    ///
    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}

/// <summary>
/// Basics of a device twin change event.
/// </summary>
public record IotHubDeviceTwinChangeEvent : AbstractIotHubEvent
{
    ///
    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}

/// <summary>
/// Basics of a device twin change event.
/// </summary>
public record IotHubDeviceConnectionStateEvent
{
    ///
    [JsonPropertyName("sequenceNumber")]
    public string? SequenceNumber { get; set; }
}

/// <summary>Abstractions for an Azure IoT Hub Event.</summary>
public abstract record AbstractIotHubEvent
{
    /// <summary>Unique identifier of the device.</summary>
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    /// <summary>Unique identifier of the module within the device.</summary>
    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; set; }

    ///
    [JsonPropertyName("etag")]
    public string? Etag { get; set; }

    /// <summary>The version.</summary>
    [JsonPropertyName("version")]
    public long Version { get; set; }

    /// <summary>The twin properties.</summary>
    [JsonPropertyName("properties")]
    public IotHubTwinPropertiesCollection? Properties { get; set; }
}

/// <summary>Properties of the device twin.</summary>
public record IotHubTwinPropertiesCollection
{
    /// 
    [JsonPropertyName("desired")]
    public IotHubTwinProperties? Desired { get; set; }

    /// 
    [JsonPropertyName("reported")]
    public IotHubTwinProperties? Reported { get; set; }
}

/// <summary>Properties of the device twin.</summary>
public record IotHubTwinProperties
{
    /// <summary>The version of the twin.</summary>
    [JsonPropertyName("$version")]
    public long Version { get; set; }

    ///
    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}
