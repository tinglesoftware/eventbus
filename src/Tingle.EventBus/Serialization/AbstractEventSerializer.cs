using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Serialization;

/// <summary>
/// Abstract implementation for an event serializer.
/// </summary>
public abstract partial class AbstractEventSerializer : IEventSerializer
{
    private const string TrimPattern = "(Serializer|EventSerializer)$";

    ///
    protected static readonly IList<string> JsonContentTypes = new[] { "application/json", "text/json", };

    private static readonly Regex trimPattern = GetTrimPattern();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="optionsAccessor"></param>
    /// <param name="loggerFactory"></param>
    protected AbstractEventSerializer(IOptionsMonitor<EventBusSerializationOptions> optionsAccessor, ILoggerFactory loggerFactory)
    {
        OptionsAccessor = optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor));

        // Create a well-scoped logger
        Name = trimPattern.Replace(GetType().Name, "");
        var categoryName = $"{LogCategoryNames.Serializers}.{Name}";
        Logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    ///
    protected string Name { get; }

    ///
    protected abstract IList<string> SupportedMediaTypes { get; }

    ///
    protected IOptionsMonitor<EventBusSerializationOptions> OptionsAccessor { get; }

    ///
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    public async Task<IEventEnvelope<T>?> DeserializeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(DeserializationContext context, CancellationToken cancellationToken = default)
        where T : class
    {
        // Assume first media type if none is specified
        var contentType = context.ContentType ?? new ContentType(SupportedMediaTypes[0]);

        // Ensure the content type is supported
        if (!SupportedMediaTypes.Contains(contentType.MediaType, StringComparer.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"The ContentType '{contentType}' is not supported by this serializer");
        }

        // Deserialize
        using var stream = context.Body.ToStream();
        var envelope = await DeserializeToEnvelopeAsync<T>(stream: stream, context: context, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (envelope is null)
        {
            Logger.DeserializationResultedInNull(identifier: context.Identifier, eventType: context.Registration.EventType.FullName);
            return null;
        }

        if (envelope.Event is null)
        {
            Logger.DeserializedEventShouldNotBeNull(identifier: context.Identifier,
                                                    eventBusId: envelope.Id,
                                                    eventType: context.Registration.EventType.FullName);
            return null;
        }

        return envelope;
    }

    /// <inheritdoc/>
    public async Task SerializeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(SerializationContext<T> context, CancellationToken cancellationToken = default) where T : class
    {
        // Assume first media type if none is specified
        context.Event.ContentType ??= new ContentType(SupportedMediaTypes[0]);

        // Ensure the content type is supported
        if (!SupportedMediaTypes.Contains(context.Event.ContentType.MediaType, StringComparer.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"The ContentType '{context.Event.ContentType}' is not supported by this serializer");
        }

        // Create the envelope for the event
        var hostInfo = OptionsAccessor.CurrentValue.HostInfo;
        var @event = context.Event;
        var envelope = new EventEnvelope<T>
        {
            Id = @event.Id,
            RequestId = @event.RequestId,
            CorrelationId = @event.CorrelationId,
            InitiatorId = @event.InitiatorId,
            Event = @event.Event,
            Expires = @event.Expires,
            Sent = @event.Sent,
            Headers = @event.Headers,
            Host = hostInfo,
        };

        // Serialize
        using var stream = new MemoryStream();
        await SerializeEnvelopeAsync(stream: stream, envelope: envelope, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Return to the beginning of the stream
        stream.Seek(0, SeekOrigin.Begin);

        context.Body = await BinaryData.FromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserialize a stream of bytes to a <see cref="EventEnvelope{T}"/>.
    /// </summary>
    /// <typeparam name="T">The event type to be deserialized.</typeparam>
    /// <param name="stream">
    /// The <see cref="Stream"/> containing the raw data.
    /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
    /// </param>
    /// <param name="context">The <see cref="DeserializationContext"/> in use.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                              DeserializationContext context,
                                                                              CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Serialize a <see cref="EventEnvelope{T}"/> into a stream of bytes.
    /// </summary>
    /// <param name="stream">
    /// The stream to serialize to.
    /// (It must be writable, i.e. <see cref="Stream.CanWrite"/> must be true).
    /// </param>
    /// <param name="envelope">The <see cref="EventEnvelope{T}"/> to be serialized.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task SerializeEnvelopeAsync<T>(Stream stream,
                                                      EventEnvelope<T> envelope,
                                                      CancellationToken cancellationToken = default)
        where T : class;

#if NET7_0_OR_GREATER
    [GeneratedRegex(TrimPattern, RegexOptions.Compiled)]
    private static partial Regex GetTrimPattern();
#else
    private static Regex GetTrimPattern() => new(TrimPattern, RegexOptions.Compiled);
#endif
}
