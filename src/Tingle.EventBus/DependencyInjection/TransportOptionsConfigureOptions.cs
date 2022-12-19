using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of <see cref="IPostConfigureOptions{TOptions}"/>
/// for shared settings in <see cref="EventBusTransportOptions"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
internal class TransportOptionsConfigureOptions<T> : IConfigureNamedOptions<T>, IPostConfigureOptions<T> where T : EventBusTransportOptions
{
    private readonly IEventBusConfigurationProvider configurationProvider;

    /// <summary>
    /// Initializes a new <see cref="TransportOptionsConfigureOptions{T}"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    public TransportOptionsConfigureOptions(IEventBusConfigurationProvider configurationProvider)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
    }

    /// <inheritdoc/>
    public void Configure(string? name, T options)
    {
        if (string.IsNullOrEmpty(name)) return;

        var configSection = configurationProvider.GetTransportConfiguration(name);
        if (configSection is null || !configSection.GetChildren().Any()) return;

        options.EmptyResultsDelay = configSection.GetValue<TimeSpan?>(nameof(options.EmptyResultsDelay)) ?? options.EmptyResultsDelay;
        options.EnableEntityCreation = configSection.GetValue<bool?>(nameof(options.EnableEntityCreation)) ?? options.EnableEntityCreation;
        options.DeadLetterSuffix = configSection.GetValue<string?>(nameof(options.DeadLetterSuffix)) ?? options.DeadLetterSuffix;
        options.DefaultEntityKind = configSection.GetValue<EntityKind?>(nameof(options.DefaultEntityKind)) ?? options.DefaultEntityKind;
        options.DefaultEventIdFormat = configSection.GetValue<EventIdFormat?>(nameof(options.DefaultEventIdFormat)) ?? options.DefaultEventIdFormat;
        options.DefaultUnhandledConsumerErrorBehaviour = configSection.GetValue<UnhandledConsumerErrorBehaviour?>(nameof(options.DefaultUnhandledConsumerErrorBehaviour)) ?? options.DefaultUnhandledConsumerErrorBehaviour;
    }

    /// <inheritdoc/>
    public void Configure(T options) => Configure(Options.Options.DefaultName, options);

    /// <inheritdoc/>
    public void PostConfigure(string? name, T options)
    {
        // check bounds for empty results delay
        var ticks = options.EmptyResultsDelay.Ticks;
        ticks = Math.Max(ticks, TimeSpan.FromSeconds(30).Ticks); // must be more than 30 seconds
        ticks = Math.Min(ticks, TimeSpan.FromMinutes(10).Ticks); // must be less than 10 minutes
        options.EmptyResultsDelay = TimeSpan.FromTicks(ticks);

        // ensure the dead-letter suffix name has been set
        if (string.IsNullOrWhiteSpace(options.DeadLetterSuffix))
        {
            throw new InvalidOperationException($"The '{nameof(options.DeadLetterSuffix)}' must be provided");
        }
    }
}
