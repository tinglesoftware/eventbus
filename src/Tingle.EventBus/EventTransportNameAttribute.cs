using System;

namespace Tingle.EventBus
{
    /// <summary>
    /// Specify the name of the transport used for this event contract/type, overriding the default one.
    /// <br />
    /// To specify the name of a custom transport, use <see cref="Transport.TransportNameAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class EventTransportNameAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">The name of the transport to use for the event contract/type</param>
        public EventTransportNameAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace", nameof(name));
            }

            Name = name;
        }

        /// <summary>
        /// The name of the transport to use for the event contract/type
        /// </summary>
        public string Name { get; }
    }
}
