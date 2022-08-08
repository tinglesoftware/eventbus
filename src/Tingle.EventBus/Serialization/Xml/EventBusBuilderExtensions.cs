using Microsoft.Extensions.Options;
using Tingle.EventBus.Serialization.Xml;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for the XML event serializer.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Use the included XML serializer as the default event serializer.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder UseDefaultXmlSerializer(this EventBusBuilder builder,
                                                          Action<XmlEventSerializerOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        // Configure the options for the serializer
        var services = builder.Services;
        if (configure != null) services.Configure(configure);
        services.ConfigureOptions<XmlEventSerializerConfigureOptions>();

        // Add the serializer
        return builder.UseDefaultSerializer<XmlEventSerializer>();
    }
}
