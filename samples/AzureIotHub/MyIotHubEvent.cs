using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureIotHub;

internal class MyIotHubEvent
{
    public int Speed { get; set; }

    [JsonExtensionData]
    public JsonElement Extras { get; set; }
}
