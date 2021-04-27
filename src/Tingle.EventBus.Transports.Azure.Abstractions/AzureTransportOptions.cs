using Azure;
using Azure.Core;
using System;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Abstraction for options of Azure-based transports
    /// </summary>
    public abstract class AzureTransportOptions : EventBusTransportOptionsBase
    {
    }
}
