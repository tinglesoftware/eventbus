using System;

namespace Tingle.EventBus.Transports.Azure.ServiceBus
{
    /// <summary>
    /// Defaults for Azure Service Bus based transport.
    /// </summary>
    public static class Defaults
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static TimeSpan LockDuration = TimeSpan.FromMinutes(5);
        public static TimeSpan DefaultMessageTimeToLive = TimeSpan.FromDays(365 + 1);
        public static TimeSpan BasicMessageTimeToLive = TimeSpan.FromDays(14);
        public static int MaxDeliveryCount = 5;

        public static TimeSpan AutoDeleteOnIdle => TimeSpan.FromDays(427);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
