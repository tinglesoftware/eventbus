using Tingle.EventBus.Transports.Kafka;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for Kafka.
/// </summary>
public static class KafkaEventBusBuilderExtensions
{
    /// <summary>Add Kafka transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddKafkaTransport(this EventBusBuilder builder, Action<KafkaTransportOptions>? configure = null)
        => builder.AddKafkaTransport(KafkaDefaults.Name, configure);

    /// <summary>Add Kafka transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddKafkaTransport(this EventBusBuilder builder, string name, Action<KafkaTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<KafkaConfigureOptions>();
        return builder.AddTransport<KafkaTransportOptions, KafkaTransport>(name, configure);
    }
}
