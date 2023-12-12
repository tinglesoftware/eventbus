using Microsoft.Extensions.DependencyInjection;
using Polly;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports;

namespace Tingle.EventBus.Internal;

internal static class ResiliencePipelineHelper
{
    public static void CombineIfNeeded(EventBusOptions busOptions, EventBusTransportOptions transportOptions, EventRegistration registration)
    {
        if (busOptions is null) throw new ArgumentNullException(nameof(busOptions));
        if (transportOptions is null) throw new ArgumentNullException(nameof(transportOptions));
        if (registration is null) throw new ArgumentNullException(nameof(registration));

        // if the pipelines have been merged, there is no need to repeat the process
        if (registration.MergedExecutionPipelines) return;

        registration.ExecutionPipeline = Combine(busOptions, transportOptions, registration);
        registration.MergedExecutionPipelines = true;
    }

    private static ResiliencePipeline Combine(EventBusOptions busOptions, EventBusTransportOptions transportOptions, EventRegistration registration)
    {
        var pipelines = new ResiliencePipeline?[] {
            busOptions.ResiliencePipeline,          // outer
            transportOptions.ResiliencePipeline,
            registration.ResiliencePipeline,        // inner
        }.Where(p => p is not null).Select(p => p!).ToArray();

        return pipelines.Length switch
        {
            0 => ResiliencePipeline.Empty,                                          // if there are none, return empty
            1 => pipelines[0],                                                      // a single pipeline can just be used (no need to combine)
            _ => new ResiliencePipelineBuilder().AddPipelines(pipelines).Build(),   // more than one needs to be combined
        };
    }

    private static ResiliencePipelineBuilder AddPipelines(this ResiliencePipelineBuilder builder, params ResiliencePipeline?[]? pipelines)
    {
        if (pipelines is null) return builder;

        foreach (var pipeline in pipelines)
        {
            if (pipeline is not null)
            {
                builder.AddPipeline(pipeline);
            }
        }

        return builder;
    }
}
