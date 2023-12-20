using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// The default implementation of <see cref="IEventSerializer"/> for JSON using the <c>System.Text.Json</c> library.
/// </summary>
/// <param name="optionsAccessor">The options for configuring the serializer.</param>
/// <param name="loggerFactory"></param>
[RequiresDynamicCode(MessageStrings.JsonSerializationRequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(MessageStrings.JsonSerializationUnreferencedCodeMessage)]
public class DefaultJsonEventSerializer(IOptionsMonitor<EventBusSerializationOptions> optionsAccessor, ILoggerFactory loggerFactory)
    : AbstractEventSerializer(optionsAccessor, loggerFactory)
{
    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => JsonContentTypes;

    /// <inheritdoc/>
    protected override async Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                    DeserializationContext context,
                                                                                    CancellationToken cancellationToken = default)
    {
        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        return await JsonSerializer.DeserializeAsync<EventEnvelope<T>>(utf8Json: stream,
                                                                       options: serializerOptions,
                                                                       cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task SerializeEnvelopeAsync<T>(Stream stream,
                                                            EventEnvelope<T> envelope,
                                                            CancellationToken cancellationToken = default)
    {
        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        await JsonSerializer.SerializeAsync(utf8Json: stream,
                                            value: envelope,
                                            options: serializerOptions,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
