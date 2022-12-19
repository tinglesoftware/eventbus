using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus;

/// <summary>Generic context for an event.</summary>
public abstract class EventContext : WrappedEventPublisher
{
    private readonly HostInfo? hostInfo;

    /// <summary>Creates an instance of <see cref="EventContext"/>.</summary>
    /// <param name="publisher">The <see cref="IEventPublisher"/> to use.</param>
    /// <param name="hostInfo">The <see cref="HostInfo"/> of the event sender.</param>
    protected EventContext(IEventPublisher publisher, HostInfo? hostInfo = null) : base(publisher)
    {
        this.hostInfo = hostInfo;
    }

    /// <summary>Creates an instance of <see cref="EventContext"/>.</summary>
    /// <param name="publisher">The <see cref="IEventPublisher"/> to use.</param>
    /// <param name="envelope">The <see cref="IEventEnvelope"/> from serialization.</param>
    /// <param name="contentType">The type of content received.</param>
    /// <param name="transportIdentifier">The unique identifier offered by the transport</param>
    protected EventContext(IEventPublisher publisher, IEventEnvelope envelope, ContentType? contentType, string? transportIdentifier)
        : this(publisher, envelope.Host)
    {
        Id = envelope.Id;
        RequestId = envelope.RequestId;
        CorrelationId = envelope.CorrelationId;
        InitiatorId = envelope.InitiatorId;
        Expires = envelope.Expires;
        Sent = envelope.Sent;
        Headers = envelope.Headers;
        ContentType = contentType;
        TransportIdentifier = transportIdentifier;
    }

    /// <summary>
    /// The unique identifier of the event.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// The unique identifier of the request associated with the event.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// A value shared between related events.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The unique identifier of the initiator of the event.
    /// </summary>
    public string? InitiatorId { get; set; }

    /// <summary>
    /// The specific time at which the event expires.
    /// </summary>
    public DateTimeOffset? Expires { get; set; }

    /// <summary>
    /// The specific time the event was sent.
    /// </summary>
    public DateTimeOffset? Sent { get; set; }

    /// <summary>
    /// The headers published alongside the event.
    /// The keys are case insensitive.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The content type used to serialize and deserialize the event to/from a stream of bytes.
    /// Setting a value, instructs the serializer how to write the event content on the transport.
    /// The serializer used for the event must support the value set.
    /// When set to <see langword="null"/>, the serializer used for the event decides what
    /// content type to use depending on its implementation.
    /// For the default implementation, see <see cref="DefaultJsonEventSerializer"/>.
    /// </summary>
    public ContentType? ContentType { get; set; }

    /// <summary>
    /// Gets or sets a key/value collection that can be used to share data within the scope of this context.
    /// </summary>
    /// <remarks>
    /// This information should not be passed on to serialization.
    /// </remarks>
    public IDictionary<string, object?> Items { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Identifier given by the transport for the event.
    /// </summary>
    public string? TransportIdentifier { get; internal init; }

    /// <summary>
    /// Gets the <see cref="HostInfo"/> of the event sender.
    /// <see langword="null"/> is returned for outgoing events.
    /// </summary>
    /// <returns></returns>
    public HostInfo? GetSenderHostInfo() => hostInfo;

    #region Header Value conversions

    /// <summary>
    /// Gets the header value associated with the specified header key
    /// and converts it to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to which to convert the value to.</typeparam>
    /// <param name="key">The header key whose value to get.</param>
    /// <param name="value">
    /// When this method returns, the value associated with the specified key, if the
    /// key is found; otherwise, the default value for <typeparamref name="T"/>.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="Headers"/> contains
    /// an element with the specified key; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidCastException">This conversion is not supported.</exception>
    /// <exception cref="FormatException">The value is not in a format recognized by <typeparamref name="T"/>.</exception>
    /// <exception cref="OverflowException">The value represents a number that is out of the range of <typeparamref name="T"/>.</exception>
    public bool TryGetHeaderValue<T>(string key, [NotNullWhen(true)] out T? value) where T : IConvertible
    {
        value = default;
        if (Headers.TryGetValue(key, out var raw_value))
        {
            // Handle nullable differently
            var t = typeof(T);
            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                if (raw_value is null) return default;
                t = Nullable.GetUnderlyingType(t);
            }

            value = (T)Convert.ChangeType(raw_value, t!);
            return true;
        }
        return default;
    }

    /// <summary>
    /// Gets the header value associated with the specified header key
    /// and converts it to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to which to convert the value to.</typeparam>
    /// <param name="key">The header key whose value to get.</param>
    /// <returns>
    /// The value associated with the specified key, if the
    /// key is found; otherwise, the default value for <typeparamref name="T"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidCastException">This conversion is not supported.</exception>
    /// <exception cref="FormatException">The value is not in a format recognized by <typeparamref name="T"/>.</exception>
    /// <exception cref="OverflowException">The value represents a number that is out of the range of <typeparamref name="T"/>.</exception>
    public T? GetHeaderValue<T>(string key) where T : IConvertible => TryGetHeaderValue<T>(key, out var value) ? value : default;

    /// <summary>
    /// Gets the header value associated with the specified header key
    /// and converts it to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to which to convert the value to.</typeparam>
    /// <param name="key">The header key whose value to get.</param>
    /// <returns>
    /// The value associated with the specified key, if the
    /// key is found; otherwise, the default value for <typeparamref name="T"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidCastException">This conversion is not supported.</exception>
    /// <exception cref="FormatException">The value is not in a format recognized by <typeparamref name="T"/>.</exception>
    /// <exception cref="OverflowException">The value represents a number that is out of the range of <typeparamref name="T"/>.</exception>
    /// <exception cref="KeyNotFoundException">The <paramref name="key"/> was not found in the headers.</exception>
    public T? GetRequiredHeaderValue<T>(string key) where T : IConvertible => GetHeaderValue<T>(key) ?? throw new KeyNotFoundException(key);

    #endregion
}

/// <summary>The context for a specific event.</summary>
/// <typeparam name="T">The type of event carried.</typeparam>
public class EventContext<T> : EventContext where T : class
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="publisher">The <see cref="IEventPublisher"/> to use.</param>
    /// <param name="event">Value for <see cref="Event"/>.</param>
    public EventContext(IEventPublisher publisher, T @event) : base(publisher)
    {
        Event = @event;
    }

    internal EventContext(IEventPublisher publisher, IEventEnvelope<T> envelope, ContentType? contentType, string? transportIdentifier)
        : base(publisher, envelope, contentType, transportIdentifier)
    {
        Event = envelope.Event!;
    }

    /// <summary>
    /// The event published or to be published.
    /// </summary>
    public T Event { get; set; }
}

/// <summary>The context for a specific dead-lettered event.</summary>
/// <typeparam name="T">The type of event carried.</typeparam>
public class DeadLetteredEventContext<T> : EventContext where T : class
{
    internal DeadLetteredEventContext(IEventPublisher publisher, IEventEnvelope<T> envelope, ContentType? contentType, string? transportIdentifier)
        : base(publisher, envelope, contentType, transportIdentifier)
    {
        Event = envelope.Event!;
    }

    /// <summary>
    /// The dead-lettered event.
    /// </summary>
    public T Event { get; set; }
}
