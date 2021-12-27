using System.Text.Json;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace AzureIotHub;

internal class AzureIotEventsConsumer : IEventConsumer<MyIotHubEvent>
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, };

    private readonly ILogger logger;

    public AzureIotEventsConsumer(ILogger<AzureIotEventsConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ConsumeAsync(EventContext<MyIotHubEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;
        var source = evt.Source;
        if (source == IotHubEventMessageSource.Telemetry)
        {
            var telemetry = evt.Telemetry;

            var deviceId = context.GetIotHubDeviceId();
            var enqueued = context.GetIotHubEnqueuedTime();

            logger.LogInformation("Received {Source} from {DeviceId}\r\nEnqueued: {EnqueuedTime}\r\nTimestamped: {Timestamp}\r\nTelemetry:{Telemetry}",
                                  source,
                                  deviceId,
                                  enqueued,
                                  telemetry?.Timestamp,
                                  JsonSerializer.Serialize(telemetry, serializerOptions));
        }
        else if (source == IotHubEventMessageSource.TwinChangeEvents)
        {
            // process twin changes here, sample to be updated in the future
        }

        return Task.CompletedTask;
    }
}
