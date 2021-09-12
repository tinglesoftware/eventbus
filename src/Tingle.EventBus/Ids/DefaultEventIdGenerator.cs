using System;
using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Ids
{
    /// <summary>
    /// Default implementation of <see cref="IEventIdGenerator"/>.
    /// </summary>
    public class DefaultEventIdGenerator : IEventIdGenerator
    {
        /// <inheritdoc/>
        public string Generate(EventRegistration reg)
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));

            var id = Guid.NewGuid();
            var bytes = id.ToByteArray();

            return reg.IdFormat switch
            {
                EventIdFormat.Guid => id.ToString(),
                EventIdFormat.GuidNoDashes => id.ToString("n"),
                EventIdFormat.Long => BitConverter.ToUInt64(bytes, 0).ToString(),
                EventIdFormat.LongHex => BitConverter.ToUInt64(bytes, 0).ToString("x"),
                EventIdFormat.DoubleLong => $"{BitConverter.ToUInt64(bytes, 0)}{BitConverter.ToUInt64(bytes, 8)}",
                EventIdFormat.DoubleLongHex => $"{BitConverter.ToUInt64(bytes, 0):x}{BitConverter.ToUInt64(bytes, 8):x}",
                EventIdFormat.Random => Convert.ToBase64String(bytes),
                _ => throw new NotSupportedException($"'{nameof(EventIdFormat)}.{reg.IdFormat}' set on event '{reg.EventType.FullName}' is not supported."),
            };
        }
    }
}
