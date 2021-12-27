namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>The type of an operational event on Azure IoT Hub.</summary>
public enum IotHubOperationalEventType
{
    ///
    UpdateTwin,

    ///
    ReplaceTwin,

    ///
    CreateDeviceIdentity,

    ///
    DeleteDeviceIdentity,

    ///
    DeviceDisconnected,

    ///
    DeviceConnected,
}
