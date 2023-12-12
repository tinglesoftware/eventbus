using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tingle.EventBus.Serialization;

namespace CustomEventSerializer;

public class AzureDevOpsEventSerializer(IOptionsMonitor<EventBusSerializationOptions> optionsAccessor, ILoggerFactory loggerFactory) : AbstractEventSerializer(optionsAccessor, loggerFactory)
{
    private readonly JsonSerializer serializer = JsonSerializer.CreateDefault();

    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => JsonContentTypes;

    /// <inheritdoc/>
    protected override Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                              DeserializationContext context,
                                                                              CancellationToken cancellationToken = default)
    {
        using var sr = new StreamReader(stream);
        using var jtr = new JsonTextReader(sr);
        var jToken = serializer.Deserialize<JToken>(jtr);

        if (jToken is null) return Task.FromResult<IEventEnvelope<T>?>(null);

        var @event = jToken.ToObject<AzureDevOpsCodePushed>();
        var envelope = new EventEnvelope<T>
        {
            Id = jToken.Value<string>("id"),
            Event = @event as T,
            Sent = jToken.Value<DateTime>("createdDate"),
        };

        // you can consider moving this to extension methods on EventContext for both get and set
        envelope.Headers["eventType"] = jToken.Value<string>("eventType")!;
        envelope.Headers["resourceVersion"] = jToken.Value<string>("resourceVersion")!;
        envelope.Headers["publisherId"] = jToken.Value<string>("publisherId")!;

        return Task.FromResult<IEventEnvelope<T>?>(envelope);
    }

    /// <inheritdoc/>
    protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                      EventEnvelope<T> envelope,
                                                      CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Serialization of AzureDevOps events should never happen.");
    }
}
