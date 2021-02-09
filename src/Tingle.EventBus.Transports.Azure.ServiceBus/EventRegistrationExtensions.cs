using System;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Extension methods on <see cref="EventRegistration"/> and <see cref="EventConsumerRegistration"/>.
    /// </summary>
    public static class EventRegistrationExtensions
    {
        internal const string MetadataKeyMapToQueue = "azure.servicebus.map-to-queue";

        /// <summary>
        /// Change mapping from topic (default) to queue and vice-versa.
        /// </summary>
        /// <param name="reg">The <see cref="EventRegistration"/> to update</param>
        /// <param name="mapToQueue">Indicates if to use a Queue instead of Topic for this event.</param>
        /// <returns></returns>
        public static EventRegistration MapToQueueInsteadOfTopic(this EventRegistration reg, bool mapToQueue = true)
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));
            reg.Metadata[MetadataKeyMapToQueue] = mapToQueue;
            return reg;
        }

        internal static bool UseQueueInsteadOfTopic(this EventRegistration reg)
        {
            return reg.Metadata.TryGetValue(MetadataKeyMapToQueue, out var val) && (bool)val;
        }
    }
}
