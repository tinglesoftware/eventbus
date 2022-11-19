using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="InMemoryTransportOptions"/>.
/// </summary>
internal class InMemoryTransportConfigureOptions : IPostConfigureOptions<InMemoryTransportOptions>
{
    private readonly EventBusOptions busOptions;

    public InMemoryTransportConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
    {
        busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
    }

    public void PostConfigure(string? name, InMemoryTransportOptions options)
    {
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
