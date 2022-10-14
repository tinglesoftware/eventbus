using Tingle.EventBus;
using Tingle.EventBus.Transports.Kafka;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Kafka.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>
    /// Add Kafka as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddKafkaTransport(this EventBusBuilder builder, Action<KafkaTransportOptions> configure)
        => builder.AddKafkaTransport(TransportNames.Kafka, configure);

    /// <summary>
    /// Add Kafka as the underlying transport for the Event Bus.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static EventBusBuilder AddKafkaTransport(this EventBusBuilder builder, string name, Action<KafkaTransportOptions> configure)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var services = builder.Services;

        // configure the options for Kafka
        services.Configure(configure);
        services.ConfigureOptions<KafkaConfigureOptions>();

        // register the transport
        builder.AddTransport<KafkaTransport, KafkaTransportOptions>(name);

        return builder;
    }
}
