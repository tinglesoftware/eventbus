namespace Tingle.EventBus;

/// <summary>
/// Name of attributes/properties/metadata added alongside a message/event if the transport supports.
/// </summary>
public static class MetadataNames
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public const string Id = "Id";
    public const string ContentType = "Content-Type";
    public const string SequenceNumber = "SequenceNumber";
    public const string Offset = "Offset";
    public const string CorrelationId = "CorrelationId";
    public const string RequestId = "RequestId";
    public const string InitiatorId = "InitiatorId";
    public const string FullyQualifiedNamespace = "FullyQualifiedNamespace";
    public const string EntityUri = "EntityUri";

    public const string ActivityId = "EventBus.ActivityId";
    public const string EventType = "EventBus.EventType";
    public const string EventName = "EventBus.EventName";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
