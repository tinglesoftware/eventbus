﻿namespace MultipleConsumers;

public class FirstEventConsumer(ILogger<FirstEventConsumer> logger) : IEventConsumer<DoorOpened>
{
    public Task ConsumeAsync(EventContext<DoorOpened> context, CancellationToken cancellationToken = default)
    {
        var evt = context.Event;
        var vehicleId = evt.VehicleId;
        var kind = evt.Kind;
        logger.LogInformation("{DoorKind} door for {VehicleId} was opened at {Opened:r}.", kind, vehicleId, evt.Opened);
        return Task.CompletedTask;
    }
}
