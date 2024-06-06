using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Serialization;

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
        builder.UseDefaultSerializer(provider =>
        {
            var optionsAccessor = provider.GetRequiredService<IOptionsMonitor<EventBusSerializationOptions>>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new DefaultJsonEventSerializer(optionsAccessor, loggerFactory);
        });

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

    /// <summary>Add Event Bus services with minimal defaults and JSON source generation.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <param name="serializerContext">
    /// The <see cref="JsonSerializerContext"/> to use for serialization.
    /// Each event to should be registered using a <see cref="IEventEnvelope{T}"/>.
    /// </param>
    /// <returns>An <see cref="EventBusBuilder"/> to continue setting up the Event Bus.</returns>
    /// <remarks>This does not include support for binding from <see cref="Configuration.IConfiguration"/>.</remarks>
    public static EventBusBuilder AddSlimEventBus(this IServiceCollection services, JsonSerializerContext serializerContext)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (serializerContext == null) throw new ArgumentNullException(nameof(serializerContext));

        var builder = services.AddSlimEventBus();
        builder.UseDefaultSerializer(provider =>
        {
            var optionsAccessor = provider.GetRequiredService<IOptionsMonitor<EventBusSerializationOptions>>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new DefaultJsonEventSerializer(serializerContext, optionsAccessor, loggerFactory);
        });

        return builder;
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

    /// <summary>Add Event Bus services with minimal defaults and JSON source generation.</summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <param name="serializerContext">
    /// The <see cref="JsonSerializerContext"/> to use for serialization.
    /// Each event to should be registered using a <see cref="IEventEnvelope{T}"/>.
    /// </param>
    /// <param name="setupAction">An optional action for setting up the bus.</param>
    /// <remarks>This does not include support for binding from <see cref="Configuration.IConfiguration"/>.</remarks>
    public static IServiceCollection AddSlimEventBus(this IServiceCollection services, JsonSerializerContext serializerContext, Action<EventBusBuilder>? setupAction = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        var builder = services.AddSlimEventBus(serializerContext);
        setupAction?.Invoke(builder);
        return services;
    }
}
