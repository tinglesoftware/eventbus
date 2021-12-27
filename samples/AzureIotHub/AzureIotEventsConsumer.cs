﻿using System.Text.Json;
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
            var lce = evt.LifeCycleEvent!;
            logger.LogInformation("Device LifeCycle event received of type '{Type}' from '{DeviceId}{ModuleId}' in '{HubName}'.\r\nEvent:{Event}",
                                  lce.Type,
                                  lce.DeviceId,
                                  lce.ModuleId,
                                  lce.HubName,
                                  JsonSerializer.Serialize(lce.Event, serializerOptions));
        }

        return Task.CompletedTask;
    }
}
