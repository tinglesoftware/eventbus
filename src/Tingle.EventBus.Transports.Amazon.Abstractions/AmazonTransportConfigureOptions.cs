using Amazon;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="AmazonTransportOptions"/> derivatives.
/// </summary>
public abstract class AmazonTransportConfigureOptions<TOptions> : IConfigureNamedOptions<TOptions>, IPostConfigureOptions<TOptions>
    where TOptions : AmazonTransportOptions
{
    private readonly IEventBusConfigurationProvider configurationProvider;

    /// <summary>
    /// Initializes a new <see cref="AmazonTransportConfigureOptions{TOptions}"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    public AmazonTransportConfigureOptions(IEventBusConfigurationProvider configurationProvider)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
    }

    /// <inheritdoc/>
    public virtual void Configure(string? name, TOptions options)
    {
        if (string.IsNullOrEmpty(name)) return;

        var configSection = configurationProvider.GetTransportConfiguration(name);
        if (configSection is null || !configSection.GetChildren().Any()) return;

        options.RegionName = configSection.GetValue<string?>(nameof(options.RegionName)) ?? options.RegionName;
        options.AccessKey = configSection.GetValue<string?>(nameof(options.AccessKey)) ?? options.AccessKey;
        options.SecretKey = configSection.GetValue<string?>(nameof(options.SecretKey)) ?? options.SecretKey;
    }

    /// <inheritdoc/>
    public virtual void Configure(TOptions options) => Configure(Options.Options.DefaultName, options);

    /// <inheritdoc/>
    public virtual void PostConfigure(string? name, TOptions options)
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
