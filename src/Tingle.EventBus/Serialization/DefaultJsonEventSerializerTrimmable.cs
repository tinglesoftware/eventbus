using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// The default implementation of <see cref="IEventSerializer"/> for JSON using the <c>System.Text.Json</c> library.
/// </summary>
public class DefaultJsonEventSerializerTrimmable : AbstractEventSerializer
{
    private readonly JsonSerializerContext serializerContext;

    /// <summary>
    /// Creates an instance of <see cref="DefaultJsonEventSerializerTrimmable"/>.
    /// </summary>
    /// <param name="serializerContext">The <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> instance to use.</param>
    /// <param name="optionsAccessor">The options for configuring the serializer.</param>
    /// <param name="loggerFactory"></param>
    public DefaultJsonEventSerializerTrimmable(JsonSerializerContext serializerContext,
                                               IOptionsMonitor<EventBusSerializationOptions> optionsAccessor,
                                               ILoggerFactory loggerFactory)
        : base(optionsAccessor, loggerFactory)
    {
        this.serializerContext = serializerContext ?? throw new ArgumentNullException(nameof(serializerContext));
    }

    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => JsonContentTypes;

    /// <inheritdoc/>
    protected override async Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                    DeserializationContext context,
                                                                                    CancellationToken cancellationToken = default)
    {
        return (EventEnvelope<T>?)await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                                        returnType: typeof(EventEnvelope<T>),
                                                                        context: serializerContext,
                                                                        cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task SerializeEnvelopeAsync<T>(Stream stream,
                                                            EventEnvelope<T> envelope,
                                                            CancellationToken cancellationToken = default)
    {
        await JsonSerializer.SerializeAsync(utf8Json: stream,
                                            value: envelope,
                                            inputType: typeof(EventEnvelope<T>),
                                            context: serializerContext,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
