using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureTransportOptions{TCredential}"/> derivatives.
/// </summary>
/// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
/// <param name="configurators">A list of <see cref="IEventBusConfigurator"/> to use when configuring options.</param>
/// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
public abstract class AzureTransportConfigureOptions<TCredential, TOptions>(IEventBusConfigurationProvider configurationProvider,
                                                                            IEnumerable<IEventBusConfigurator> configurators,
                                                                            IOptions<EventBusOptions> busOptionsAccessor)
    : EventBusTransportConfigureOptions<TOptions>(configurationProvider, configurators, busOptionsAccessor)
    where TCredential : AzureTransportCredentials
    where TOptions : AzureTransportOptions<TCredential>
{
    /// <inheritdoc/>
    public override ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var result = base.Validate(name, options);
        if (!result.Succeeded) return result;

        // We should either have a token credential or a connection string
        if (options.Credentials.CurrentValue is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.Credentials)}' must be provided in form a connection string or an instance of '{typeof(TCredential).Name}'.");
        }

        // We must have TokenCredential if using TCredential
        if (options.Credentials.CurrentValue is TCredential tc && tc.TokenCredential is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(tc.TokenCredential)}' must be provided when using '{typeof(TCredential).Name}'.");
        }

        return ValidateOptionsResult.Success;
    }
}
