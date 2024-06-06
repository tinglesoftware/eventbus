using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// The default implementation of <see cref="IEventSerializer"/> for JSON using the <c>System.Text.Json</c> library.
/// </summary>
public class DefaultJsonEventSerializer : AbstractEventSerializer
{
    private readonly JsonSerializerContext? serializerContext;

    /// <summary>
    /// </summary>
    /// <param name="optionsAccessor">The options for configuring the serializer.</param>
    /// <param name="loggerFactory"></param>
    [RequiresDynamicCode(MessageStrings.JsonSerializationRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(MessageStrings.JsonSerializationUnreferencedCodeMessage)]
    public DefaultJsonEventSerializer(IOptionsMonitor<EventBusSerializationOptions> optionsAccessor, ILoggerFactory loggerFactory) : base(optionsAccessor, loggerFactory) { }

    /// <summary>
    /// </summary>
    /// <param name="serializerContext"></param>
    /// <param name="optionsAccessor">The options for configuring the serializer.</param>
    /// <param name="loggerFactory"></param>
    public DefaultJsonEventSerializer(JsonSerializerContext serializerContext, IOptionsMonitor<EventBusSerializationOptions> optionsAccessor, ILoggerFactory loggerFactory)
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
        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        if (serializerContext is null)
        {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            return await JsonSerializer.DeserializeAsync<EventEnvelope<T>>(utf8Json: stream,
                                                                         options: serializerOptions,
                                                                         cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        }
        else
        {
            return await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                         returnType: typeof(EventEnvelope<T>),
                                                         context: serializerContext,
                                                         cancellationToken: cancellationToken).ConfigureAwait(false) as EventEnvelope<T>;
        }
    }

    /// <inheritdoc/>
    protected override async Task SerializeEnvelopeAsync<T>(Stream stream,
                                                            EventEnvelope<T> envelope,
                                                            CancellationToken cancellationToken = default)
    {
        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        if (serializerContext is null)
        {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            await JsonSerializer.SerializeAsync(utf8Json: stream,
                                                value: envelope,
                                                options: serializerOptions,
                                                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        }
        else
        {
            await JsonSerializer.SerializeAsync(utf8Json: stream,
                                                value: envelope,
                                                inputType: typeof(EventEnvelope<T>),
                                                context: serializerContext,
                                                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
