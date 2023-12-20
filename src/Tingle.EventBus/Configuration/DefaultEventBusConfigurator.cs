using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Transports;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Default implementation of <see cref="IEventBusConfigurator"/>.
/// </summary>
/// <param name="configurationProvider">The <see cref="IEventBusConfigurationProvider"/> instance.</param>
[RequiresDynamicCode(MessageStrings.BindingDynamicCodeMessage)]
[RequiresUnreferencedCode(MessageStrings.BindingUnreferencedCodeMessage)]
internal class DefaultEventBusConfigurator(IEventBusConfigurationProvider configurationProvider) : IEventBusConfigurator
{
    /// <inheritdoc/>
    public void Configure(EventBusOptions options)
    {
        configurationProvider.Configuration.Bind(options);
    }

    /// <inheritdoc/>
    public void Configure<TOptions>(IConfiguration configuration, TOptions options) where TOptions : EventBusTransportOptions
    {
        configuration.Bind(options);
    }

    /// <inheritdoc/>
    public void Configure(EventRegistration registration, EventBusOptions options)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));
        if (options is null) throw new ArgumentNullException(nameof(options));

        // bind from IConfiguration
        var configuration = configurationProvider.Configuration.GetSection($"Events:{registration.EventType.FullName}");
        configuration.Bind(registration);
        foreach (var ecr in registration.Consumers)
        {
            configuration.GetSection($"Consumers:{ecr.ConsumerType.FullName}").Bind(ecr);
        }
    }
}
