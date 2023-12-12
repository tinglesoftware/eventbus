namespace CustomEventConfigurator;

public class SampleConsumer2(ILogger<SampleConsumer2> logger) : IEventConsumer<SampleEvent2>
{
    public Task ConsumeAsync(EventContext<SampleEvent2> context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received event Id: {Id}", context.Id);
        logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
        return Task.CompletedTask;
    }
}
