using System.Text.Json.Serialization;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

[JsonSerializable(typeof(IotHubConnectionAuthMethod))]
internal partial class IotHubJsonSerializerContext : JsonSerializerContext { }
