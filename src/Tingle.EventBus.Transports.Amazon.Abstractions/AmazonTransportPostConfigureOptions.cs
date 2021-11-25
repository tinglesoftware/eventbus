using Amazon;
using Amazon.Runtime;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AmazonTransportOptions"/> derivatives.
/// </summary>
public abstract class AmazonTransportPostConfigureOptions<TOptions> : IPostConfigureOptions<TOptions> where TOptions : AmazonTransportOptions
{
    /// <inheritdoc/>
    public virtual void PostConfigure(string name, TOptions options)
    {
        // Ensure the region is provided
        if (string.IsNullOrWhiteSpace(options.RegionName) && options.Region == null)
        {
            throw new InvalidOperationException($"Either '{nameof(options.RegionName)}' or '{nameof(options.Region)}' must be provided");
        }

        options.Region ??= RegionEndpoint.GetBySystemName(options.RegionName);

        /*
         * If the credentials have not been provided,
         * we need the AccessKey and SecretKey to be provided for u
         * to create a basic credential
        */
        if (options.Credentials is null)
        {
            // Ensure the access key is specified
            // Ensure the secret is specified
            if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
            {
                throw new InvalidOperationException(
                    $"Both '{nameof(options.AccessKey)}' and '{nameof(options.SecretKey)}' must be provided when '{nameof(options.Credentials)}' is not provided.");
            }

            // Create the basic credentials
            options.Credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        }
    }
}
