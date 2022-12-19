using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of <see cref="IPostConfigureOptions{TOptions}"/>
/// for shared settings in <see cref="EventBusTransportOptions"/>.
/// </summary>
/// <typeparam name="TOptions"></typeparam>
public abstract class EventBusTransportConfigureOptions<TOptions> : IConfigureNamedOptions<TOptions>, IPostConfigureOptions<TOptions>, IValidateOptions<TOptions>
    where TOptions : EventBusTransportOptions
{
    private readonly IEventBusConfigurationProvider configurationProvider;

    /// <summary>
    /// Initializes a new <see cref="EventBusTransportConfigureOptions{TOptions}"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    public EventBusTransportConfigureOptions(IEventBusConfigurationProvider configurationProvider)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
    }

    /// <inheritdoc/>
    public virtual void Configure(string? name, TOptions options)
    {
        if (string.IsNullOrEmpty(name)) return;

        var configuration = configurationProvider.Configuration.GetSection($"Transports:{name}");
        if (configuration.GetChildren().Any()) Configure(configuration, options);
    }

    /// <summary>
    /// Invoked to configure an instance of <typeparamref name="TOptions"/> with the relevant configuration
    /// provided by <see cref="IConfigurationProvider"/> for the specific transport by name.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="options"></param>
    protected virtual void Configure(IConfiguration configuration, TOptions options)
    {
        configuration.Bind(options);
    }

    /// <inheritdoc/>
    public virtual void Configure(TOptions options) => Configure(Options.Options.DefaultName, options);

    /// <inheritdoc/>
    public virtual void PostConfigure(string? name, TOptions options)
    {
        // check bounds for empty results delay
        var ticks = options.EmptyResultsDelay.Ticks;
        ticks = Math.Max(ticks, TimeSpan.FromSeconds(30).Ticks); // must be more than 30 seconds
        ticks = Math.Min(ticks, TimeSpan.FromMinutes(10).Ticks); // must be less than 10 minutes
        options.EmptyResultsDelay = TimeSpan.FromTicks(ticks);
    }

    /// <inheritdoc/>
    public virtual ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // Ensure we have WaitTransportStarted set
        if (options.WaitTransportStarted is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.WaitTransportStarted)}' must be provided.");
        }

        // ensure the dead-letter suffix name has been set
        if (string.IsNullOrWhiteSpace(options.DeadLetterSuffix))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.DeadLetterSuffix)}' must be provided.");
        }

        return ValidateOptionsResult.Success;
    }
}
