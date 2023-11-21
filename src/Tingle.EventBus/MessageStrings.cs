namespace Tingle.EventBus;

internal class MessageStrings
{
    public const string XmlSerializationUnreferencedCodeMessage = "XML serialization and deserialization might require types that cannot be statically analyzed.";

    public const string JsonSerializationUnreferencedCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed. Use the serialziers and types that takes a JsonSerializerContext, or make sure all of the required types are preserved.";
    public const string JsonSerializationRequiresDynamicCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.";
}
