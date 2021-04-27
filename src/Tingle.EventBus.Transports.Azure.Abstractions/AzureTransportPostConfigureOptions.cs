using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AzureTransportOptions{TCredenetial}"/> derivatives.
    /// </summary>
    public abstract class AzureTransportPostConfigureOptions<TCredenetial, TOptions> : IPostConfigureOptions<TOptions>
        where TCredenetial : AzureTransportCredentials
        where TOptions : AzureTransportOptions<TCredenetial>
    {
        /// <inheritdoc/>
        public virtual void PostConfigure(string name, TOptions options)
        {
            // We should either have a token credential or a connection string
            if (options.Credentials is null || options.Credentials.Value is null)
            {
                throw new InvalidOperationException($"'{nameof(options.Credentials)}' must be provided in form a connection string or an instance of '{typeof(TCredenetial).Name}'.");
            }
        }
    }
}
