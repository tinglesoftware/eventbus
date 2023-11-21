using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tingle.EventBus.Transports;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Abstraction for configuring instances of <see cref="EventRegistration"/>.
/// </summary>
public interface IEventConfigurator
{
    /// <summary>
    /// Configure an instance of <see cref="EventRegistration"/>.
    /// This is called once for each event either during startup
    /// (when there is a consumer) or on first publish.
    /// </summary>
    /// <param name="registration">The <see cref="EventRegistration"/> to be configured.</param>
    /// <param name="options">The <see cref="EventBusOptions"/> instance.</param>
    void Configure(EventRegistration registration, EventBusOptions options);
}

/// <summary>
/// Abstraction for configuring instances of <see cref="EventBusOptions"/>.
/// </summary>
public interface IEventBusConfigurator
{
    /// <summary>
    /// Configure an instance of <see cref="EventBusOptions"/>.
    /// This is called once during startup.
    /// </summary>
    /// <param name="options">The <see cref="EventBusOptions"/> instance.</param>
    void Configure(EventBusOptions options);

    /// <summary>
    /// Configure options for a specific transport.
    /// </summary>
    /// <typeparam name="TOptions">The type of options.</typeparam>
    /// <param name="configuration"></param>
    /// <param name="options">The <typeparamref name="TOptions"/> instance to be configured.</param>
    void Configure<TOptions>(IConfiguration configuration, TOptions options) where TOptions : EventBusTransportOptions;
}
