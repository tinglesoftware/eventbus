using System;
namespace Tingle.EventBus.Abstractions
{
    internal class EventConsumerRegistration
    {
        public EventConsumerRegistration(Type eventType, Type consumerType)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            ConsumerType = consumerType ?? throw new ArgumentNullException(nameof(consumerType));
        }

        public Type EventType { get; }
        public Type ConsumerType { get; }
    }
}
