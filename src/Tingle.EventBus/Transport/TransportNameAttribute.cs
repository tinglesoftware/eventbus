using System;

namespace Tingle.EventBus.Transport
{
    /// <summary>
    /// Specify the name used for a transport.
    /// This is required for each concrete implementation of <see cref="IEventBusTransport"/>.
    /// <br />
    /// To specify the transport to be used on an event, use <see cref="EventTransportNameAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class TransportNameAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">The name of the transport.</param>
        public TransportNameAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace", nameof(name));
            }

            Name = name;
        }

        /// <summary>
        /// The name of the transport.
        /// </summary>
        public string Name { get; }
    }
}
