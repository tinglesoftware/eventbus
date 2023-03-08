using System.Text.Json.Serialization;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace AzureIotHub;

public record MyIotHubEvent : IotHubEvent<MyIotHubTelemetry>
{
    public MyIotHubEvent(IotHubEventMessageSource source,
                         MyIotHubTelemetry? telemetry,
                         IotHubOperationalEvent<IotHubDeviceTwinChangeEvent>? twinEvent,
                         IotHubOperationalEvent<IotHubDeviceLifecycleEvent>? lifecycleEvent,
                         IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>? connectionStateEvent)
        : base(source, telemetry, twinEvent, lifecycleEvent, connectionStateEvent) { }
}

public class MyIotHubTelemetry
{
    public DateTimeOffset Timestamp { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extras { get; set; }
}
