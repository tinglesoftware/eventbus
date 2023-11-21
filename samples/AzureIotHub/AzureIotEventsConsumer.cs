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
        if (evt.IsTelemetry)
        {
            var telemetry = evt.GetTelemetry<MyIotHubTelemetry>();
            var deviceId = context.GetIotHubDeviceId();
            var enqueued = context.GetIotHubEnqueuedTime();

            logger.LogInformation("Received Telemetry from {DeviceId}\r\nEnqueued: {EnqueuedTime}\r\nTimestamped: {Timestamp}\r\nTelemetry:{Telemetry}",
                                  deviceId,
                                  enqueued,
                                  telemetry.Timestamp,
                                  JsonSerializer.Serialize(telemetry, serializerOptions));
        }
        else
        {
            var prefix = evt.Source switch
            {
                IotHubEventMessageSource.TwinChangeEvents => "TwinChange",
                IotHubEventMessageSource.DeviceLifecycleEvents => "Device Lifecycle",
                IotHubEventMessageSource.DeviceConnectionStateEvents => "Device connection state",
                _ => throw new InvalidOperationException($"Unknown event source '{evt.Source}'."),
            };
            var ope = evt.Event;
            logger.LogInformation("{Prefix} event received of type '{Type}' from '{DeviceId}{ModuleId}' in '{HubName}'.\r\nEvent:{Event}",
                                  prefix,
                                  ope.Type,
                                  ope.DeviceId,
                                  ope.ModuleId,
                                  ope.HubName,
                                  JsonSerializer.Serialize(ope.Payload, serializerOptions));
        }

        return Task.CompletedTask;
    }
}
