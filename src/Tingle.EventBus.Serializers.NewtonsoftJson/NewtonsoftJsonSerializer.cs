using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Serializers;

/// <summary>
/// Implementation of <see cref="IEventSerializer"/> that uses <c>Newtonsoft.Json</c>.
/// </summary>
public class NewtonsoftJsonSerializer : AbstractEventSerializer
{
    private readonly JsonSerializer serializer;

    /// <summary>
    /// Creates an instance of <see cref="NewtonsoftJsonSerializer"/>.
    /// </summary>
    /// <param name="serializerOptionsAccessor">The options for configuring the serializer.</param>
    /// <param name="optionsAccessor"></param>
    /// <param name="loggerFactory"></param>
    public NewtonsoftJsonSerializer(IOptions<NewtonsoftJsonSerializerOptions> serializerOptionsAccessor,
                                    IOptionsMonitor<EventBusSerializationOptions> optionsAccessor,
                                    ILoggerFactory loggerFactory)
        : base(optionsAccessor, loggerFactory)
    {
        var settings = serializerOptionsAccessor?.Value?.SerializerSettings ?? throw new ArgumentNullException(nameof(serializerOptionsAccessor));
        serializer = JsonSerializer.Create(settings);
    }

    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => JsonContentTypes;

    /// <inheritdoc/>
    protected override Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                              DeserializationContext context,
                                                                              CancellationToken cancellationToken = default)
    {
        // get the encoding and always default to UTF-8
        var encoding = Encoding.GetEncoding(context.ContentType?.CharSet ?? Encoding.UTF8.BodyName);

        // Deserialize
        using var sr = new StreamReader(stream: stream,
                                        encoding: encoding,
                                        detectEncodingFromByteOrderMarks: true,
                                        bufferSize: 512,
                                        leaveOpen: true);
        using var jr = new JsonTextReader(sr);
        var envelope = serializer.Deserialize<EventEnvelope<T>>(jr);
        return Task.FromResult<IEventEnvelope<T>?>(envelope);
    }

    /// <inheritdoc/>
    protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                      EventEnvelope<T> envelope,
                                                      CancellationToken cancellationToken = default)
    {
        // Serialize
        using var sw = new StreamWriter(stream: stream,
                                        encoding: Encoding.UTF8,
                                        bufferSize: 512,
                                        leaveOpen: true);
        using var jw = new JsonTextWriter(sw);
        serializer.Serialize(jw, envelope);

        return Task.CompletedTask;
    }
}
