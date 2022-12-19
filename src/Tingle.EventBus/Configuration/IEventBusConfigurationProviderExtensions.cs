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
    /// <param name="transportType">The type of transport</param>
    /// <returns>The specified <see cref="IConfiguration"/> object, or null if the requested section does not exist.</returns>
    public static IConfiguration GetTransportConfiguration(this IEventBusConfigurationProvider provider, string transportName, string? transportType = null)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));

        if (provider.EventBusConfiguration is null)
        {
            throw new InvalidOperationException("There was no top-level EventBus property found in configuration.");
        }

        var key = $"{EventBusTransportsKey}:{transportName}";
        if (transportType is not null) key += $":{transportType}";
        return provider.EventBusConfiguration.GetSection(key);
    }
}
