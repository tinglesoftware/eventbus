namespace AzureManagedIdentity;

internal class VehicleTelemetryEventsConsumer(ILogger<VehicleTelemetryEventsConsumer> logger) : IEventConsumer<VehicleTelemetryEvent>
{
    public async Task ConsumeAsync(EventContext<VehicleTelemetryEvent> context, CancellationToken cancellationToken)
    {
        var telemetry = context.Event;

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

        var timestamp = telemetry.Timestamp;
        var updateEvt = new VehicleDoorOpenedEvent
        {
            VehicleId = telemetry.DeviceId,
            Kind = kind.Value,
            Closed = status is VehicleDoorStatus.Closed ? timestamp : null,
            Opened = status is VehicleDoorStatus.Open ? timestamp : null,
        };

        // the VehicleDoorOpenedEvent on a broadcast bus would notify all subscribers
        await context.PublishAsync(updateEvt, cancellationToken: cancellationToken);
    }
}
