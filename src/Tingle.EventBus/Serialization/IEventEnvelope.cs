using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// An envelope of a event as represented when serialized.
    /// </summary>
    public interface IEventEnvelope
    {
        /// <summary>
        /// The unique identifier of the event.
        /// </summary>
        string? Id { get; }

        /// <summary>
        /// The unique identifier of the request associated with the event.
        /// </summary>
        string? RequestId { get; }

        /// <summary>
        /// A value shared between related events.
        /// </summary>
        string? CorrelationId { get; }

        /// <summary>
        /// The unique identifier of the initiator of the event.
        /// </summary>
        string? InitiatorId { get; }

        /// <summary>
        /// The specific time at which the event expires.
        /// </summary>
        DateTimeOffset? Expires { get; }

        /// <summary>
        /// The specific time the event was sent.
        /// </summary>
        DateTimeOffset? Sent { get; }

        /// <summary>
        /// The headers published alongside the event.
        /// </summary>
        IDictionary<string, object> Headers { get; }

        /// <summary>
        /// Information about the host on which the event was generated.
        /// </summary>
        HostInfo? Host { get; }
    }

    /// <summary>
    /// An envelope of a event as represented when serialized.
    /// </summary>
    public interface IEventEnvelope<T> : IEventEnvelope where T : class
    {
        /// <summary>
        /// The event published or to be published.
        /// </summary>
        T? Event { get; }
    }
}
