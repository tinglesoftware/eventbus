namespace Tingle.EventBus.Diagnostics
{
    internal static class ActivityTagNames
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

        public const string EventBusEventType = "eventbus.event_type";
        public const string EventBusSerializerType = "eventbus.serializer_type";
        public const string EventBusConsumerType = "eventbus.consumer_type";
    }
}
