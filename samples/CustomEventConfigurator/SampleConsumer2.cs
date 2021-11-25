using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace CustomEventConfigurator;

public class SampleConsumer2 : IEventConsumer<SampleEvent2>
{
    private readonly ILogger logger;

    public SampleConsumer2(ILogger<SampleConsumer2> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ConsumeAsync(EventContext<SampleEvent2> context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received event Id: {Id}", context.Id);
        logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
        return Task.CompletedTask;
    }
}
