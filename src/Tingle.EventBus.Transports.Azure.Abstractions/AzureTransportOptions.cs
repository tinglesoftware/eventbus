using System;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Abstraction for options of Azure-based transports
    /// </summary>
    public abstract class AzureTransportOptions<TCredenetial> : EventBusTransportOptionsBase where TCredenetial : AzureTransportCredentials
    {
        /// <summary>
        /// Authentication credentials.
        /// This can either be a connection string or <typeparamref name="TCredenetial"/>.
        /// </summary>
        public AnyOf<TCredenetial, string> Credentials { get; set; }
    }
}
