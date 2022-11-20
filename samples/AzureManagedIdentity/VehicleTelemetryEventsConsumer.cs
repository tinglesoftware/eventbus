using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace AzureManagedIdentity;

internal class VehicleTelemetryEventsConsumer : IEventConsumer<VehicleTelemetryEvent>
{
    private readonly ILogger logger;

    public VehicleTelemetryEventsConsumer(ILogger<VehicleTelemetryEventsConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync(EventContext<VehicleTelemetryEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;
        var source = evt.Source;
        if (source != IotHubEventMessageSource.Telemetry) return;

        var telemetry = evt.Telemetry!;
        var action = telemetry.Action;
        if (action is not "door-status-changed")
        {
            logger.LogWarning("Telemetry with action '{TelemetryAction}' is not yet supported", action);
            return;
        }

        var status = telemetry.VehicleDoorStatus;
        if (status is not VehicleDoorStatus.Open and not VehicleDoorStatus.Closed)
        {
            logger.LogWarning("Vehicle Door status '{VehicleDoorStatus}' is not yet supported", status);
            return;
        }

        var kind = telemetry.VehicleDoorKind;
        if (kind is null)
        {
            logger.LogWarning("Vehicle Door kind '{VehicleDoorKind}' cannot be null", kind);
            return;
        }

        var deviceId = context.GetIotHubDeviceId();
        var timestamp = telemetry.Timestamp;
        var updateEvt = new VehicleDoorOpenedEvent
        {
            VehicleId = deviceId, // not the registration number
            Kind = kind.Value,
            Closed = status is VehicleDoorStatus.Closed ? timestamp : null,
            Opened = status is VehicleDoorStatus.Open ? timestamp : null,
        };

        // the VehicleDoorOpenedEvent on a broadcast bus would notify all subscribers
        await context.PublishAsync(updateEvt, cancellationToken: cancellationToken);
    }
}
