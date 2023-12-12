namespace SimpleConsumer;

public class SampleEventConsumer(EventCounter counter, ILogger<SampleEventConsumer> logger) : IEventConsumer<SampleEvent>
{
    public Task ConsumeAsync(EventContext<SampleEvent> context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received event Id: {Id}", context.Id);
        logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
        counter.Consumed();
        return Task.CompletedTask;
    }
}
