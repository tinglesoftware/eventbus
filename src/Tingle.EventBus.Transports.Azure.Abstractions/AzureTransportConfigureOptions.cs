using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureTransportOptions{TCredential}"/> derivatives.
/// </summary>
public abstract class AzureTransportConfigureOptions<TCredential, TOptions> : TransportOptionsConfigureOptions<TOptions>
    where TCredential : AzureTransportCredentials
    where TOptions : AzureTransportOptions<TCredential>
{
    /// <summary>
    /// Initializes a new <see cref="AzureTransportConfigureOptions{TCredential, TOptions}"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    public AzureTransportConfigureOptions(IEventBusConfigurationProvider configurationProvider) : base(configurationProvider) { }

    /// <inheritdoc/>
    public override ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var result = base.Validate(name, options);
        if (!result.Succeeded) return result;

        // We should either have a token credential or a connection string
        if (options.Credentials == default || options.Credentials.CurrentValue is null)
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
