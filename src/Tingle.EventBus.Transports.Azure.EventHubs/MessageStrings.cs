namespace Tingle.EventBus.Transports.Azure.EventHubs;

internal class MessageStrings
{
    public const string JsonSerializationUnreferencedCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed. Use the serialziers and types that takes a JsonSerializerContext, or make sure all of the required types are preserved.";
    public const string JsonSerializationRequiresDynamicCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.";
}
