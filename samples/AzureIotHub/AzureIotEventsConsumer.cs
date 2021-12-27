using System.Text.Json;

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
        var telemetry = evt.Telemetry;

        var deviceId = context.GetIotHubDeviceId();
        var source = context.GetIotHubMessageSource();
        var enqueued = context.GetIotHubEnqueuedTime();

        logger.LogInformation("Received {Source} from {DeviceId}\r\nEnqueued: {EnqueuedTime}\r\nTimestamped: {Timestamp}\r\nTelemetry:{Telemetry}",
                              source,
                              deviceId,
                              enqueued,
                              telemetry?.Timestamp,
                              JsonSerializer.Serialize(telemetry, serializerOptions));

        return Task.CompletedTask;
    }
}
