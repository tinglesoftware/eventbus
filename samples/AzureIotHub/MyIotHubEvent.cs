using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.EventBus.Configuration;

namespace AzureIotHub;

[EventSerializer(typeof(MyIotHubEventSerializer))]
internal class MyIotHubEvent
{
    public DateTimeOffset Timestamp { get; set; }

    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}
