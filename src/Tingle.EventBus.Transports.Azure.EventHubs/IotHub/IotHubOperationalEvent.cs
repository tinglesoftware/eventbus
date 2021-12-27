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
public record DeviceLifecycleEvent
{
    // TODO: consider adding more items here

    ///
    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}

/// <summary>
/// Basics of a device twin change event.
/// </summary>
public record DeviceTwinChangeEvent
{
    /// <summary>
    /// The version of the twin.
    /// This value is auto incremented by the service.
    /// </summary>
    public long Version { get; set; }

    // TODO: consider adding more items here e.g. expose reported and desired as peroperties?

    ///
    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}
