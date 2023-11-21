using System.Text.Json.Serialization;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace AzureIotHub;

public record MyIotHubEvent : IotHubEvent { }

public class MyIotHubTelemetry
{
    public DateTimeOffset Timestamp { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extras { get; set; }
}
