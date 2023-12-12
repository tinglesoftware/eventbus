namespace CustomEventConfigurator;

public class SampleConsumer1(ILogger<SampleConsumer1> logger) : IEventConsumer<SampleEvent1>
{
    public Task ConsumeAsync(EventContext<SampleEvent1> context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received event Id: {Id}", context.Id);
        logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
        return Task.CompletedTask;
    }
}
