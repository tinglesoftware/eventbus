using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Implementation of <see cref="IPostConfigureOptions{TOptions}"/>
    /// for shared settings in <see cref="EventBusTransportOptionsBase"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class TransportOptionsPostConfigureOptions<T> : IPostConfigureOptions<EventBusTransportOptionsBase>
    {
        public void PostConfigure(string name, EventBusTransportOptionsBase options)
        {
            // Ensure delay is within 30sec and 10min bounds
            if (options.EmptyResultsDelay < TimeSpan.FromSeconds(30) || options.EmptyResultsDelay > TimeSpan.FromMinutes(10))
            {
                throw new InvalidOperationException($"The '{nameof(options.EmptyResultsDelay)}' must be between 30 seconds and 10 minutes.");
            }
        }
    }
}
