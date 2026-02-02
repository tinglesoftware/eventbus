namespace Tingle.EventBus;

internal class MessageStrings
{
    public const string XmlSerializationUnreferencedCodeMessage = "XML serialization and deserialization might require types that cannot be statically analyzed.";
    public const string XmlSerializationRequiresDynamicCodeMessage = "XML serialization and deserialization might require types that cannot be statically analyzed.";

    public const string JsonSerializationUnreferencedCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed. Use the serializers and types that takes a JsonSerializerContext, or make sure all of the required types are preserved.";
    public const string JsonSerializationRequiresDynamicCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.";

    public const string BindingUnreferencedCodeMessage = "In case the type is non-primitive, the trimmer cannot statically analyze the object's type so its members may be trimmed.";
    public const string BindingDynamicCodeMessage = "Binding strongly typed objects to configuration values requires generating dynamic code at runtime, for example instantiating generic types.";

    public const string RequiresUnreferencedCodeMessage = "JSON serialization, deserialization, and binding strongly typed objects to configuration values might require types that cannot be statically analyzed.";
    public const string RequiresDynamicCodeMessage = "JSON serialization, deserialization, and binding strongly typed objects to configuration values might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.";

    public const string GenericsUnreferencedCodeMessage = "The native code for this instantiation might require types that cannot be statically analyzed.";
    public const string GenericsDynamicCodeMessage = "The native code for this instantiation might not be available at runtime.";
}
