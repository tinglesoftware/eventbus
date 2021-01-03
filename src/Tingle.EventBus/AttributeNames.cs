namespace Tingle.EventBus
{
    /// <summary>
    /// Attribute and property names added alongside a message/event if the transport supports
    /// </summary>
    internal static class AttributeNames
    {
        public const string Id = "Id";
        public const string ContentType = "Content-Type";
        public const string SequenceNumber = "SequenceNumber";
        public const string CorrelationId = "CorrelationId";
        public const string RequestId = "RequestId";
        public const string InitiatorId = "InitiatorId";

        public const string ActivityId = "EventBus.ActivityId";
        public const string EventType = "EventBus.EventType";
        public const string EventName = "EventBus.EventName";
    }
}
