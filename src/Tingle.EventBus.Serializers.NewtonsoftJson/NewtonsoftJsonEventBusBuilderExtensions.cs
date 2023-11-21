using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Serializers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for NewtonsoftJson serializer.
/// </summary>
[RequiresUnreferencedCode(MessageStrings.UnreferencedCodeMessage)]
public static class NewtonsoftJsonEventBusBuilderExtensions
{
    /// <summary>
    /// Use the included NewtonsoftJson serializer as the default event serializer.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder UseDefaultNewtonsoftJsonSerializer(this EventBusBuilder builder,
                                                                     Action<NewtonsoftJsonSerializerOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        // Configure the options for the serializer
        var services = builder.Services;
        if (configure != null) services.Configure(configure);
        services.ConfigureOptions<NewtonsoftJsonSerializerConfigureOptions>();

        // Add the serializer
        return builder.UseDefaultSerializer<NewtonsoftJsonSerializer>();
    }
}
