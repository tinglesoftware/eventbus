namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>The kind of source for an Azure IoT Hub Event.</summary>
public enum IotHubEventMessageSource
{
    ///
    Telemetry,

    ///
    TwinChangeEvents,

    ///
    DeviceLifecycleEvents,
}
