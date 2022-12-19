﻿using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="InMemoryTransportOptions"/>.
/// </summary>
internal class InMemoryTransportConfigureOptions : EventBusTransportConfigureOptions<InMemoryTransportOptions>
{
    private readonly EventBusOptions busOptions;

    /// <summary>
    /// Initializes a new <see cref="InMemoryTransportConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>\
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public InMemoryTransportConfigureOptions(IEventBusConfigurationProvider configurationProvider, IOptions<EventBusOptions> busOptionsAccessor)
        : base(configurationProvider)
    {
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    /// <inheritdoc/>
    public override void PostConfigure(string? name, InMemoryTransportOptions options)
    {
        base.PostConfigure(name, options);
        if (name is null) throw new ArgumentNullException(nameof(name));

        var registrations = busOptions.GetRegistrations(name);
        foreach (var reg in registrations)
        {
            // Set the IdFormat
            options.SetEventIdFormat(reg, busOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast, EntityKind.Queue);
        }
    }
}
