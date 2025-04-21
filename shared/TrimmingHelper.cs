using System.Diagnostics.CodeAnalysis;

namespace Tingle.EventBus.Internal;

internal static class TrimmingHelper
{
    internal const DynamicallyAccessedMemberTypes Serializer = DynamicallyAccessedMemberTypes.PublicConstructors;
    internal const DynamicallyAccessedMemberTypes Transport = DynamicallyAccessedMemberTypes.PublicConstructors;
    internal const DynamicallyAccessedMemberTypes Configurator = DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors;
    internal const DynamicallyAccessedMemberTypes Event = DynamicallyAccessedMemberTypes.PublicConstructors;
    internal const DynamicallyAccessedMemberTypes Consumer = DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors;
}
