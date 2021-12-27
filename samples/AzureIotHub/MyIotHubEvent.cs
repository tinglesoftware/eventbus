using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace AzureIotHub;

internal record MyIotHubEvent : IotHubEvent<MyIotHubTelemetry>
{
    public MyIotHubEvent(IotHubEventMessageSource source,
                         MyIotHubTelemetry? telemetry,
                         IotHubOperationalEvent<DeviceTwinChangeEvent>? twinEvent,
                         IotHubOperationalEvent<DeviceLifeCycleEvent>? lifeCycleEvent)
        : base(source, telemetry, twinEvent, lifeCycleEvent) { }
}

internal class MyIotHubTelemetry
{
    public DateTimeOffset Timestamp { get; set; }

    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}
