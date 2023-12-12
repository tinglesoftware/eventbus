using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="InMemoryTransportOptions"/>.
/// </summary>
/// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
/// <param name="configurators">A list of <see cref="IEventBusConfigurator"/> to use when configuring options.</param>
/// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
internal class InMemoryTransportConfigureOptions(IEventBusConfigurationProvider configurationProvider,
                                                 IEnumerable<IEventBusConfigurator> configurators,
                                                 IOptions<EventBusOptions> busOptionsAccessor)
    : EventBusTransportConfigureOptions<InMemoryTransportOptions>(configurationProvider, configurators, busOptionsAccessor)
{
    /// <inheritdoc/>
    public override void PostConfigure(string? name, InMemoryTransportOptions options)
    {
        base.PostConfigure(name, options);
        if (name is null) throw new ArgumentNullException(nameof(name));

        var registrations = BusOptions.GetRegistrations(name);
        foreach (var reg in registrations)
        {
            // Set the values using defaults
            options.SetValuesUsingDefaults(reg, BusOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast, EntityKind.Queue);
        }
    }
}
