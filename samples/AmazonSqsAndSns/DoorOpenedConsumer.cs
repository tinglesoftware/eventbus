namespace AmazonSqsAndSns;

public class DoorOpenedConsumer : IEventConsumer<DoorOpened>
{
    private readonly ILogger logger;

    public DoorOpenedConsumer(ILogger<DoorOpenedConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ConsumeAsync(EventContext<DoorOpened> context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received event Id: {Id}", context.Id);
        logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
        return Task.CompletedTask;
    }
}
