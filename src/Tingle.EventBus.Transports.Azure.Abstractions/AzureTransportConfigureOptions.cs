using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureTransportOptions{TCredential}"/> derivatives.
/// </summary>
public abstract class AzureTransportConfigureOptions<TCredential, TOptions> : IPostConfigureOptions<TOptions>, IValidateOptions<TOptions>
    where TCredential : AzureTransportCredentials
    where TOptions : AzureTransportOptions<TCredential>
{
    /// <inheritdoc/>
    public virtual void PostConfigure(string? name, TOptions options)
    {
        // intentionally left bank for future use
    }

    /// <inheritdoc/>
    public virtual ValidateOptionsResult Validate(string? name, TOptions options)
    {
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
