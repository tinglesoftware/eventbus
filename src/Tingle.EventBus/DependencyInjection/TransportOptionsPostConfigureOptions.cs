using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of <see cref="IPostConfigureOptions{TOptions}"/>
/// for shared settings in <see cref="EventBusTransportOptionsBase"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
internal class TransportOptionsPostConfigureOptions<T> : IPostConfigureOptions<T> where T : EventBusTransportOptionsBase
{
    public void PostConfigure(string name, T options)
    {
        // check bounds for empty results delay
        var ticks = options.EmptyResultsDelay.Ticks;
        ticks = Math.Max(ticks, TimeSpan.FromSeconds(30).Ticks); // must be more than 30 seconds
        ticks = Math.Min(ticks, TimeSpan.FromMinutes(10).Ticks); // must be less than 10 minutes
        options.EmptyResultsDelay = TimeSpan.FromTicks(ticks);

        // ensure the deadletter suffix name has been set
        if (string.IsNullOrWhiteSpace(options.DeadLetterSuffix))
        {
            throw new InvalidOperationException($"The '{nameof(options.DeadLetterSuffix)}' must be provided");
        }
    }
}
