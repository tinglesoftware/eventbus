using System.Text.Json.Serialization;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

[JsonSerializable(typeof(IotHubConnectionAuthMethod))]
[JsonSerializable(typeof(IotHubOperationalEventPayload))]
internal partial class IotHubJsonSerializerContext : JsonSerializerContext { }
