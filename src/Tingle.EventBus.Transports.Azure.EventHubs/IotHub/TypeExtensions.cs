using System.Diagnostics.CodeAnalysis;

namespace System;

internal static class TypeExtensions
{
    public static bool IsAssignableFromGeneric(this Type genericType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type givenType)
    {
        var interfaceTypes = givenType.GetInterfaces();

        foreach (var it in interfaceTypes)
        {
            if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
                return true;
        }

        if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
            return true;

        var baseType = givenType.BaseType;
        return baseType != null && genericType.IsAssignableFromGeneric(baseType);
    }
}
