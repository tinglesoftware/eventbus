using Microsoft.Extensions.DependencyInjection;
using Polly;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports;

namespace Tingle.EventBus;

internal static class PollyHelper
{
    public static void Combine(EventBusOptions busOptions, EventBusTransportOptions transportOptions, EventRegistration registration)
    {
        if (busOptions is null) throw new ArgumentNullException(nameof(busOptions));
        if (transportOptions is null) throw new ArgumentNullException(nameof(transportOptions));
        if (registration is null) throw new ArgumentNullException(nameof(registration));

        // if the policies have been merged, there is no need to repeat the process
        if (registration.MergedExecutionPolicies) return;

        registration.ExecutionPolicy = CombineInternal(busOptions, transportOptions, registration);
        registration.MergedExecutionPolicies = true;
    }

    private static IAsyncPolicy CombineInternal(EventBusOptions busOptions, EventBusTransportOptions transportOptions, EventRegistration registration)
    {
        var policies = new IAsyncPolicy?[] {
            busOptions.RetryPolicy,          // outer
            transportOptions.RetryPolicy,
            registration.RetryPolicy,               // inner
        }.Where(p => p is not null).Select(p => p!).ToArray();

        return policies.Length switch
        {
            0 => Policy.NoOpAsync(),            // if there are none, return No-Op, if
            1 => policies[0],                   // a single policy can just be used (no need to combine)
            _ => Policy.WrapAsync(policies),    // more than one needs to be combined
        };
    }
}
