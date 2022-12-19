using Microsoft.Extensions.Configuration;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Provides an interface for implementing a construct that provides
/// access to EventBus-related configuration sections.
/// </summary>
public interface IEventBusConfigurationProvider
{
    /// <summary>
    /// Gets the <see cref="IConfigurationSection"/> where EventBus options are stored.
    /// </summary>
    IConfiguration Configuration { get; }
}
