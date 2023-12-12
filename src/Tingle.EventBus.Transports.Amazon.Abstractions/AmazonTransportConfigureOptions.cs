using Amazon;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AmazonTransportOptions"/> derivatives.
/// </summary>
/// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
/// <param name="configurators">A list of <see cref="IEventBusConfigurator"/> to use when configuring options.</param>
/// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
public abstract class AmazonTransportConfigureOptions<TOptions>(IEventBusConfigurationProvider configurationProvider,
                                                                IEnumerable<IEventBusConfigurator> configurators,
                                                                IOptions<EventBusOptions> busOptionsAccessor)
    : EventBusTransportConfigureOptions<TOptions>(configurationProvider, configurators, busOptionsAccessor) where TOptions : AmazonTransportOptions
{
    /// <inheritdoc/>
    public override void PostConfigure(string? name, TOptions options)
    {
        base.PostConfigure(name, options);

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
