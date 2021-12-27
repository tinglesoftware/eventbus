using Microsoft.Extensions.Options;
using System.Text.Json;
using Tingle.EventBus.Serialization;

namespace AzureIotHub;

internal class MyIotHubEventSerializer : AbstractEventSerializer
{
    public MyIotHubEventSerializer(IOptionsMonitor<EventBusOptions> optionsAccessor,
                                   ILoggerFactory loggerFactory)
        : base(optionsAccessor, loggerFactory) { }

    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => JsonContentTypes;

    /// <inheritdoc/>
    protected override async Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                    DeserializationContext context,
                                                                                    CancellationToken cancellationToken = default)
    {
        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        var @event = await JsonSerializer.DeserializeAsync<T>(utf8Json: stream,
                                                              options: serializerOptions,
                                                              cancellationToken: cancellationToken);

        return new EventEnvelope<T> { Event = @event, };
    }

    /// <inheritdoc/>
    protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                      EventEnvelope<T> envelope,
                                                      CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Serialization of IotHubEvent events should never happen.");
    }
}
