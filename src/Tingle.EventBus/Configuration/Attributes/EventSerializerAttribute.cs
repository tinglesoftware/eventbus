using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Specify the serializer type used for an event contract/type, overriding the default one.
/// </summary>
/// <param name="serializerType">
/// The type of serializer to use for the event type.
/// It must implement <see cref="Serialization.IEventSerializer"/>.
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class EventSerializerAttribute([DynamicallyAccessedMembers(TrimmingHelper.Serializer)] Type serializerType) : Attribute
{
    /// <summary>
    /// The type of serializer to be used.
    /// </summary>
    [DynamicallyAccessedMembers(TrimmingHelper.Serializer)]
    public Type SerializerType { get; } = serializerType ?? throw new ArgumentNullException(nameof(serializerType));
}
