using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.InMemory;

/// <summary>Defaults for <see cref="InMemoryTransportOptions"/>.</summary>
public static class InMemoryDefaults
{
    /// <summary>Default name for <see cref="InMemoryTransportOptions"/>.</summary>
    public const string Name = "inmemory";
}
