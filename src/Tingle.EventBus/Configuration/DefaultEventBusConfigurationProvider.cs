using Microsoft.Extensions.Configuration;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Default implementation of <see cref="IEventBusConfigurationProvider"/>.
/// </summary>
internal class DefaultEventBusConfigurationProvider : IEventBusConfigurationProvider
{
    private readonly IConfiguration configuration;
    private const string EventBusKey = "EventBus";

    public DefaultEventBusConfigurationProvider(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IConfiguration EventBusConfiguration => configuration.GetSection(EventBusKey);
}