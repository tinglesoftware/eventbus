using Microsoft.Extensions.Configuration;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Default implementation of <see cref="IEventBusConfigurationProvider"/>.
/// </summary>
internal class DefaultEventBusConfigurationProvider(IConfiguration configuration) : IEventBusConfigurationProvider
{
    private const string EventBusKey = "EventBus";

    /// <inheritdoc/>
    public IConfiguration Configuration => configuration.GetSection(EventBusKey);
}