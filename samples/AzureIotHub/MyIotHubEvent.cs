using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace AzureIotHub;

internal record MyIotHubEvent : IotHubEvent<MyIotHubTelemetry>
{
    public MyIotHubEvent(IotHubEventMessageSource source,
                         MyIotHubTelemetry? telemetry,
                         IotHubOperationalEvent<IotHubDeviceTwinChangeEvent>? twinEvent,
                         IotHubOperationalEvent<IotHubDeviceLifecycleEvent>? lifecycleEvent,
                         IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>? connectionStateEvent)
        : base(source, telemetry, twinEvent, lifecycleEvent, connectionStateEvent) { }
}

internal class MyIotHubTelemetry
{
    public DateTimeOffset Timestamp { get; set; }

    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}
