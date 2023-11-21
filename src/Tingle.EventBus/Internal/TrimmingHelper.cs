using System.Diagnostics.CodeAnalysis;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Tingle.EventBus.Internal;

public static class TrimmingHelper
{
    public const DynamicallyAccessedMemberTypes Serializer = DynamicallyAccessedMemberTypes.PublicConstructors;
    public const DynamicallyAccessedMemberTypes Transport = DynamicallyAccessedMemberTypes.PublicConstructors;
    public const DynamicallyAccessedMemberTypes Configurator = DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors;
    public const DynamicallyAccessedMemberTypes Event = DynamicallyAccessedMemberTypes.PublicConstructors;
    public const DynamicallyAccessedMemberTypes Consumer = DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors;
}
