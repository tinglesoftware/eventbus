using Microsoft.Extensions.Configuration;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Extension methods for <see cref="IEventBusConfigurationProvider"/>
/// </summary>
public static class IEventBusConfigurationProviderExtensions
{
    private const string EventBusTransportsKey = "Transports";

    /// <summary>Returns the specified <see cref="IConfiguration"/> object.</summary>
    /// <param name="provider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>
    /// <param name="transportName">The path to the section to be returned.</param>
    /// <returns>The specified <see cref="IConfiguration"/> object, or null if the requested section does not exist.</returns>
    public static IConfiguration GetTransportConfiguration(this IEventBusConfigurationProvider provider, string transportName)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));

        if (provider.EventBusConfiguration is null)
        {
            throw new InvalidOperationException("There was no top-level EventBus property found in configuration.");
        }

        return provider.EventBusConfiguration.GetSection($"{EventBusTransportsKey}:{transportName}");
    }
}
