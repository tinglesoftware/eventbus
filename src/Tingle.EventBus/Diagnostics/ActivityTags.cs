namespace Tingle.EventBus.Diagnostics
{
    internal static class ActivityTags
    {
        public const string MessagingSystem = "messaging.system";
        public const string MessagingMessageId = "messaging.message_id";
        public const string MessagingConversationId = "messaging.conversation_id";
        public const string MessagingDestination = "messaging.destination";
        public const string MessagingDestinationKind = "messaging.destination_kind";
        public const string MessagingTempDestination = "messaging.temp_destination";
        public const string MessagingProtocol = "messaging.protocol";
        public const string MessagingProtocolVersion = "messaging.protocol_version";
        public const string MessagingUrl = "messaging.url";

        public const string NetPeerIp = "net.peer.ip";
        public const string NetPeerName = "net.peer.name";

        public const string EventType = "event.type";
        public const string SerializerType = "serializer.type";
        public const string ConsumerType = "consumer.type";
    }
}
