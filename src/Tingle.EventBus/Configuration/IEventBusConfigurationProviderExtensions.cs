using Microsoft.Extensions.Configuration;

namespace Tingle.EventBus.Configuration;

/// <summary>Extension methods for <see cref="IEventBusConfigurationProvider"/>.</summary>
internal static class IEventBusConfigurationProviderExtensions
{
    private const string EventBusTransportsKey = "Transports";

    /// <summary>Gets the <see cref="IConfigurationSection"/> for the specified transport.</summary>
    /// <param name="provider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>
    /// <param name="name">The name of the transport whose configuration section is to be returned.</param>
    /// <returns>The specified <see cref="IConfiguration"/> object, or null if the requested section does not exist.</returns>
    public static IConfiguration GetForTransport(this IEventBusConfigurationProvider provider, string name)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));

        var config = provider.Configuration;
        if (config is null)
        {
            throw new InvalidOperationException("There was no top-level EventBus property found in configuration.");
        }

        return config.GetSection($"{EventBusTransportsKey}:{name}");
    }
}
