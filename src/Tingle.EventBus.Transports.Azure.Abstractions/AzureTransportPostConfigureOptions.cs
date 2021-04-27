using Azure;
using Azure.Core;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="AzureTransportOptions"/> derivatives.
    /// </summary>
    public abstract class AzureTransportPostConfigureOptions<TOptions> : IPostConfigureOptions<TOptions> where TOptions : AzureTransportOptions
    {
        /// <inheritdoc/>
        public virtual void PostConfigure(string name, TOptions options)
        {
        }
    }
}
