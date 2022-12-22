namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>Represents an event from Azure IoT Hub.</summary>
public record IotHubEvent<TDeviceTelemetry> : IotHubEvent<TDeviceTelemetry, IotHubDeviceTwinChangeEvent, IotHubDeviceLifecycleEvent>
    where TDeviceTelemetry : class
{
    /// <summary>
    /// Creates an instance of <see cref="IotHubEvent{TDeviceTelemetry}"/>.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="telemetry">The telemetry data.</param>
    /// <param name="twinEvent">The twin change event.</param>
    /// <param name="lifecycleEvent">The lifecycle event.</param>
    /// <param name="connectionStateEvent">The connection state event.</param>
    public IotHubEvent(IotHubEventMessageSource source,
                       TDeviceTelemetry? telemetry,
                       IotHubOperationalEvent<IotHubDeviceTwinChangeEvent>? twinEvent,
                       IotHubOperationalEvent<IotHubDeviceLifecycleEvent>? lifecycleEvent,
                       IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>? connectionStateEvent)
        : base(source, telemetry, twinEvent, lifecycleEvent, connectionStateEvent) { }
}

/// <summary>
/// Represents the event from Azure IoT Hub
/// </summary>
public record IotHubEvent<TDeviceTelemetry, TDeviceTwinChange, TDeviceLifecycle>
    where TDeviceTelemetry : class
    where TDeviceTwinChange : IotHubDeviceTwinChangeEvent
    where TDeviceLifecycle : IotHubDeviceLifecycleEvent
{
    /// <summary>
    /// Creates an instance of <see cref="IotHubEvent{TDeviceTelemetry, TDeviceTwinChange, TDeviceLifecycle}"/>.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="telemetry">The telemetry data.</param>
    /// <param name="twinEvent">The twin change event.</param>
    /// <param name="lifecycleEvent">The lifecycle event.</param>
    /// <param name="connectionStateEvent">The connection state event.</param>
    public IotHubEvent(IotHubEventMessageSource source,
                       TDeviceTelemetry? telemetry,
                       IotHubOperationalEvent<TDeviceTwinChange>? twinEvent,
                       IotHubOperationalEvent<TDeviceLifecycle>? lifecycleEvent,
                       IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>? connectionStateEvent)
    {
        Source = source;
        Telemetry = telemetry;
        TwinEvent = twinEvent;
        LifecycleEvent = lifecycleEvent;
        ConnectionStateEvent = connectionStateEvent;
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
    /// The lifecycle event.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.DeviceLifecycleEvents"/>.
    /// </summary>
    public IotHubOperationalEvent<TDeviceLifecycle>? LifecycleEvent { get; }

    /// <summary>
    /// The connection state event.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.DeviceConnectionStateEvents"/>.
    /// </summary>
    public IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>? ConnectionStateEvent { get; }
}
