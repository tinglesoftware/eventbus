﻿using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="IServiceCollection"/> for EventBus.
/// </summary>
public static class EventBusServiceCollectionExtensions
{
    /// <summary>Add Event Bus services.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <returns>An <see cref="EventBusBuilder"/> to continue setting up the Event Bus.</returns>
    [RequiresDynamicCode(MessageStrings.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(MessageStrings.RequiresUnreferencedCodeMessage)]
    public static EventBusBuilder AddEventBus(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        var builder = new EventBusBuilder(services);
        services.AddSingleton<IEventBusConfigurator, DefaultEventBusConfigurator>(); // can be multiple do not use TryAdd*(...)

        return builder;
    }

    /// <summary>Add Event Bus services.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <param name="setupAction">An optional action for setting up the bus.</param>
    [RequiresDynamicCode(MessageStrings.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(MessageStrings.RequiresUnreferencedCodeMessage)]
    public static IServiceCollection AddEventBus(this IServiceCollection services, Action<EventBusBuilder>? setupAction = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        var builder = services.AddEventBus();
        setupAction?.Invoke(builder);
        return services;
    }

    /// <summary>Add Event Bus services with minimal defaults.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <returns>An <see cref="EventBusBuilder"/> to continue setting up the Event Bus.</returns>
    /// <remarks>
    /// This does not include support for:
    /// <list type="number">
    /// <item>binding from <see cref="Configuration.IConfiguration"/></item>
    /// <item>JSON serialization and deserialization.</item>
    /// </list>
    /// </remarks>
    public static EventBusBuilder AddSlimEventBus(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        return new EventBusBuilder(services);
    }

    /// <summary>Add Event Bus services with minimal defaults.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <param name="setupAction">An optional action for setting up the bus.</param>
    /// <remarks>
    /// This does not include support for:
    /// <list type="number">
    /// <item>binding from <see cref="Configuration.IConfiguration"/></item>
    /// <item>JSON serialization and deserialization.</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddSlimEventBus(this IServiceCollection services, Action<EventBusBuilder>? setupAction = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        var builder = services.AddSlimEventBus();
        setupAction?.Invoke(builder);
        return services;
    }
}
