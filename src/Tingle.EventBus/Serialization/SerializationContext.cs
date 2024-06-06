using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Serialization;

/// <summary>Context for performing serialization.</summary>
/// <param name="event">The event to be serialized.</param>
/// <param name="registration">Registration for this event being deserialized.</param>
public sealed class SerializationContext<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(EventContext<T> @event, EventRegistration registration) where T : class
{
    /// <summary>
    /// The event to be serialized.
    /// </summary>
    public EventContext<T> Event { get; } = @event ?? throw new ArgumentNullException(nameof(@event));

    /// <summary>
    /// The <see cref="BinaryData"/> containing the raw data.
    /// </summary>
    public BinaryData? Body { get; internal set; }

    /// <summary>
    /// Registration for this event being deserialized.
    /// </summary>
    public EventRegistration Registration { get; } = registration ?? throw new ArgumentNullException(nameof(registration));
}
