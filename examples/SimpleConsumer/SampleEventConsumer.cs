using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace SimpleConsumer
{
    public class SampleEventConsumer : IEventBusConsumer<SampleEvent>
    {
        private readonly ILogger logger;

        public SampleEventConsumer(ILogger<SampleEventConsumer> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ConsumeAsync(EventContext<SampleEvent> context, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Received event Id: {Id}", context.Id);
            logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
            return Task.CompletedTask;
        }
    }
}
