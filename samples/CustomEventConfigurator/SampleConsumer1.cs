using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace CustomEventConfigurator
{
    public class SampleConsumer1 : IEventConsumer<SampleEvent1>
    {
        private readonly ILogger logger;

        public SampleConsumer1(ILogger<SampleConsumer1> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ConsumeAsync(EventContext<SampleEvent1> context, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Received event Id: {Id}", context.Id);
            logger.LogInformation("Event body: {EventBody}", System.Text.Json.JsonSerializer.Serialize(context.Event));
            return Task.CompletedTask;
        }
    }
}
