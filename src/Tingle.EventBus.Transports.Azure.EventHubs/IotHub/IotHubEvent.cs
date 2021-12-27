using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>Represents an event from Azure IoT Hub.</summary>
public interface IIotHubEvent<TDeviceTelemetry, TDeviceTwinChange, TDeviceLifeCycle>
    where TDeviceTelemetry : class, new()
    where TDeviceTwinChange : DeviceTwinChangeEvent, new()
    where TDeviceLifeCycle : DeviceLifeCycleEvent, new()
{
    /// <summary>The source of the event.</summary>
    IotHubEventMessageSource Source { get; }

    /// <summary>
    /// The telemetry data.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.Telemetry"/>.
    /// </summary>
    TDeviceTelemetry? Telemetry { get; }

    /// <summary>
    /// The twin change event.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.TwinChangeEvents"/>.
    /// </summary>
    IotHubOperationalEvent<TDeviceTwinChange>? TwinEvent { get; }

    /// <summary>
    /// The life cycle event.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.DeviceLifecycleEvents"/>.
    /// </summary>
    IotHubOperationalEvent<TDeviceLifeCycle>? LifeCycleEvent { get; }
}

/// <summary>Represents an event from Azure IoT Hub.</summary>
public sealed record IotHubEvent<TDeviceTelemetry> : IIotHubEvent<TDeviceTelemetry, DeviceTwinChangeEvent, DeviceLifeCycleEvent>
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
    {
        Source = source;
        Telemetry = telemetry;
        TwinEvent = twinEvent;
        LifeCycleEvent = lifeCycleEvent;
    }

    /// <inheritdoc/>
    public IotHubEventMessageSource Source { get; }

    /// <inheritdoc/>
    public TDeviceTelemetry? Telemetry { get; }

    /// <inheritdoc/>
    public IotHubOperationalEvent<DeviceTwinChangeEvent>? TwinEvent { get; }

    /// <inheritdoc/>
    public IotHubOperationalEvent<DeviceLifeCycleEvent>? LifeCycleEvent { get; }
}

/// <summary>
/// Represents the event from Azure IoT Hub
/// </summary>
[EventSerializer(typeof(IotHubEventSerializer))]
public sealed record IotHubEvent<TDeviceTelemetry, TDeviceTwinChange, TDeviceLifeCycle> : IIotHubEvent<TDeviceTelemetry, TDeviceTwinChange, TDeviceLifeCycle>
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

    /// <inheritdoc/>
    public IotHubEventMessageSource Source { get; }

    /// <inheritdoc/>
    public TDeviceTelemetry? Telemetry { get; }

    /// <inheritdoc/>
    public IotHubOperationalEvent<TDeviceTwinChange>? TwinEvent { get; }

    /// <inheritdoc/>
    public IotHubOperationalEvent<TDeviceLifeCycle>? LifeCycleEvent { get; }
}
