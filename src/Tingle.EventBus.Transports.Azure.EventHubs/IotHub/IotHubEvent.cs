namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>Represents an event from Azure IoT Hub.</summary>
public record IotHubEvent<TDeviceTelemetry> : IotHubEvent<TDeviceTelemetry, DeviceTwinChangeEvent, DeviceLifeCycleEvent>
    where TDeviceTelemetry : class, new()
{
    /// <summary>
    /// Creates an instance of <see cref="IotHubEvent{TDeviceTelemetry}"/>.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="telemetry">The telemetry data.</param>
    /// <param name="twinEvent">The twin change event.</param>
    /// <param name="lifeCycleEvent">The life cycle event.</param>
    public IotHubEvent(IotHubEventMessageSource source,
                       TDeviceTelemetry? telemetry,
                       IotHubOperationalEvent<DeviceTwinChangeEvent>? twinEvent,
                       IotHubOperationalEvent<DeviceLifeCycleEvent>? lifeCycleEvent)
        : base(source, telemetry, twinEvent, lifeCycleEvent)    {    }
}

/// <summary>
/// Represents the event from Azure IoT Hub
/// </summary>
public record IotHubEvent<TDeviceTelemetry, TDeviceTwinChange, TDeviceLifeCycle>
    where TDeviceTelemetry : class, new()
    where TDeviceTwinChange : DeviceTwinChangeEvent, new()
    where TDeviceLifeCycle : DeviceLifeCycleEvent, new()
{
    /// <summary>
    /// Creates an instance of <see cref="IotHubEvent{TDeviceTelemetry, TDeviceTwinChange, TDeviceLifeCycle}"/>.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="telemetry">The telemetry data.</param>
    /// <param name="twinEvent">The twin change event.</param>
    /// <param name="lifeCycleEvent">The life cycle event.</param>
    public IotHubEvent(IotHubEventMessageSource source,
                       TDeviceTelemetry? telemetry,
                       IotHubOperationalEvent<TDeviceTwinChange>? twinEvent,
                       IotHubOperationalEvent<TDeviceLifeCycle>? lifeCycleEvent)
    {
        Source = source;
        Telemetry = telemetry;
        TwinEvent = twinEvent;
        LifeCycleEvent = lifeCycleEvent;
    }

    /// <summary>The source of the event.</summary>
    public IotHubEventMessageSource Source { get; }

    /// <summary>
    /// The telemetry data.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.Telemetry"/>.
    /// </summary>
    public TDeviceTelemetry? Telemetry { get; }

    /// <summary>
    /// The twin change event.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.TwinChangeEvents"/>.
    /// </summary>
    public IotHubOperationalEvent<TDeviceTwinChange>? TwinEvent { get; }

    /// <summary>
    /// The life cycle event.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.DeviceLifecycleEvents"/>.
    /// </summary>
    public IotHubOperationalEvent<TDeviceLifeCycle>? LifeCycleEvent { get; }
}
