using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AzureTransportOptions{TCredential}"/> derivatives.
/// </summary>
public abstract class AzureTransportPostConfigureOptions<TCredential, TOptions> : IPostConfigureOptions<TOptions>
    where TCredential : AzureTransportCredentials
    where TOptions : AzureTransportOptions<TCredential>
{
    /// <inheritdoc/>
    public virtual void PostConfigure(string name, TOptions options)
    {
        // We should either have a token credential or a connection string
        if (options.Credentials is null || options.Credentials.Value is null)
        {
            throw new InvalidOperationException($"'{nameof(options.Credentials)}' must be provided in form a connection string or an instance of '{typeof(TCredential).Name}'.");
        }

        // We must have TokenCredential if using TCredential
        if (options.Credentials.Value is TCredential tc && tc.TokenCredential is null)
        {
            throw new InvalidOperationException($"'{nameof(tc.TokenCredential)}' must be provided when using '{typeof(TCredential).Name}'.");
        }
    }
}
