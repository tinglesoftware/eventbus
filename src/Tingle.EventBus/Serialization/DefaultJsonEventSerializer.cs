using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// The default implementation of <see cref="IEventSerializer"/> for JSON using the <c>System.Text.Json</c> library.
/// </summary>
/// <remarks>
/// </remarks>
/// <param name="optionsAccessor">The options for configuring the serializer.</param>
/// <param name="loggerFactory"></param>
public class DefaultJsonEventSerializer(IOptionsMonitor<EventBusSerializationOptions> optionsAccessor, ILoggerFactory loggerFactory) : AbstractEventSerializer(optionsAccessor, loggerFactory)
{
    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => JsonContentTypes;

    /// <inheritdoc/>
    protected override async Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(
        Stream stream,
        DeserializationContext context,
        CancellationToken cancellationToken = default)
    {
        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        var jsonTypeInfo = (JsonTypeInfo<EventEnvelope<T>>)serializerOptions.GetTypeInfo(typeof(EventEnvelope<T>));
        return await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                     jsonTypeInfo: jsonTypeInfo,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);

    }

    /// <inheritdoc/>
    protected override async Task SerializeEnvelopeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(
        Stream stream,
        EventEnvelope<T> envelope,
        CancellationToken cancellationToken = default)
    {
        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        var jsonTypeInfo = (JsonTypeInfo<EventEnvelope<T>>)serializerOptions.GetTypeInfo(typeof(EventEnvelope<T>));
        await JsonSerializer.SerializeAsync(utf8Json: stream,
                                            value: envelope,
                                            jsonTypeInfo: jsonTypeInfo,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);

    }
}
