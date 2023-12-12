namespace AmazonSqsAndSns;

public class DoorOpenedConsumer(ILogger<DoorOpenedConsumer> logger) : IEventConsumer<DoorOpened>
{
    public Task ConsumeAsync(EventContext<DoorOpened> context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received event Id: {Id}", context.Id);
        logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
        return Task.CompletedTask;
    }
}
