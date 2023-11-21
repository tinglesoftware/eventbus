namespace Tingle.EventBus.Serializers;

internal class MessageStrings
{
    public const string UnreferencedCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed. Migrate to System.Text.Json which has support for native AOT applications.";
}
