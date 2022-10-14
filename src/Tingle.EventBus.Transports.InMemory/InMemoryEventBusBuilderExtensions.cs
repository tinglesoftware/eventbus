using Tingle.EventBus.Transports.InMemory;
using Tingle.EventBus.Transports.InMemory.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for InMemory.
/// </summary>
public static class InMemoryEventBusBuilderExtensions
{
    /// <summary>Add InMemory transport.</summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddInMemoryTransport(this EventBusBuilder builder, Action<InMemoryTransportOptions>? configure = null)
        => builder.AddInMemoryTransport(InMemoryDefaults.Name, configure);

    /// <summary>Add InMemory transport.</summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddInMemoryTransport(this EventBusBuilder builder, string name, Action<InMemoryTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<InMemoryTransportConfigureOptions>();
        builder.Services.AddSingleton<SequenceNumberGenerator>();
        return builder.AddTransport<InMemoryTransportOptions, InMemoryTransport>(name, configure);
    }

    /// <summary>
    /// Add InMemory test harness. This can be resolved using <see cref="InMemoryTestHarness"/>.
    /// <br/>
    /// Ensure the InMemory transport has been added using
    /// <see cref="AddInMemoryTransport(EventBusBuilder, Action{InMemoryTransportOptions})"/>
    /// before resolving instances of <see cref="InMemoryTestHarness"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddInMemoryTestHarness(this EventBusBuilder builder, Action<InMemoryTestHarnessOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var services = builder.Services;

        // Configure the options for InMemory test harness
        if (configure is not null) services.Configure(configure);
        services.ConfigureOptions<InMemoryTestHarnessConfigureOptions>();

        // Register the harness
        services.AddSingleton<InMemoryTestHarness>();

        return builder;
    }
}
