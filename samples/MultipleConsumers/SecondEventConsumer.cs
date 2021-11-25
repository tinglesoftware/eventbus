namespace MultipleConsumers;

public class SecondEventConsumer : IEventConsumer<DoorOpened>
{
    private readonly ILogger logger;

    public SecondEventConsumer(ILogger<SecondEventConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ConsumeAsync(EventContext<DoorOpened> context, CancellationToken cancellationToken = default)
    {
        var evt = context.Event;
        var vehicleId = evt.VehicleId;
        var kind = evt.Kind;
        logger.LogInformation("{DoorKind} door for {VehicleId} was opened at {Opened:r}.", kind, vehicleId, evt.Opened);
        return Task.CompletedTask;
    }
}
