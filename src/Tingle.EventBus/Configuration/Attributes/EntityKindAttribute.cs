namespace Tingle.EventBus.Configuration;

/// <summary>
/// Specify the EntityKind used for this event contract/type, overriding the generated one.
/// </summary>
/// <param name="kind">The event kind to use for the event.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class EntityKindAttribute(EntityKind kind) : Attribute
{
    /// <summary>
    /// The name of the event mapped
    /// </summary>
    public EntityKind Kind { get; } = kind;
}
