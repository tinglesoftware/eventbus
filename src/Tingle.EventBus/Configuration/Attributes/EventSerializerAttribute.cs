using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Specify the serializer type used for an event contract/type, overriding the default one.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class EventSerializerAttribute : Attribute
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="serializerType">
    /// The type of serializer to use for the event type.
    /// It must implement <see cref="Serialization.IEventSerializer"/>.
    /// </param>
    public EventSerializerAttribute([DynamicallyAccessedMembers(TrimmingHelper.Serializer)] Type serializerType)
    {
        SerializerType = serializerType ?? throw new ArgumentNullException(nameof(serializerType));

        // do not check if it implements IEventSerializer here, it shall be done in the validation of options
    }

    /// <summary>
    /// The type of serializer to be used.
    /// </summary>
    [DynamicallyAccessedMembers(TrimmingHelper.Serializer)]
    public Type SerializerType { get; }
}
