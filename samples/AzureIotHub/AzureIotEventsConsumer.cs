using System.Text.Json;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace AzureIotHub;

[ConsumerName("$Default")] // or [ConsumerName(EventHubConsumerClient.DefaultConsumerGroupName)]
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
            var telemetry = evt.Telemetry!;
            var deviceId = context.GetIotHubDeviceId();
            var enqueued = context.GetIotHubEnqueuedTime();

            logger.LogInformation("Received {Source} from {DeviceId}\r\nEnqueued: {EnqueuedTime}\r\nTimestamped: {Timestamp}\r\nTelemetry:{Telemetry}",
                                  source,
                                  deviceId,
                                  enqueued,
                                  telemetry.Timestamp,
                                  JsonSerializer.Serialize(telemetry, serializerOptions));
        }
        else if (source == IotHubEventMessageSource.TwinChangeEvents)
        {
            var @tce = evt.TwinEvent!;
            logger.LogInformation("TwinChange event received of type '{Type}' from '{DeviceId}{ModuleId}' in '{HubName}'.\r\nEvent:{Event}",
                                  tce.Type,
                                  tce.DeviceId,
                                  tce.ModuleId,
                                  tce.HubName,
                                  JsonSerializer.Serialize(tce.Event, serializerOptions));
        }
        else if (source == IotHubEventMessageSource.DeviceLifecycleEvents)
        {
            var lce = evt.LifecycleEvent!;
            logger.LogInformation("Device Lifecycle event received of type '{Type}' from '{DeviceId}{ModuleId}' in '{HubName}'.\r\nEvent:{Event}",
                                  lce.Type,
                                  lce.DeviceId,
                                  lce.ModuleId,
                                  lce.HubName,
                                  JsonSerializer.Serialize(lce.Event, serializerOptions));
        }
        else if (source == IotHubEventMessageSource.DeviceConnectionStateEvents)
        {
            var cse = evt.ConnectionStateEvent!;
            logger.LogInformation("Device connection state event received of type '{Type}' from '{DeviceId}{ModuleId}' in '{HubName}'.\r\nEvent:{Event}",
                                  cse.Type,
                                  cse.DeviceId,
                                  cse.ModuleId,
                                  cse.HubName,
                                  JsonSerializer.Serialize(cse.Event, serializerOptions));
        }

        return Task.CompletedTask;
    }
}
