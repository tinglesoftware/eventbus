using Tingle.EventBus;
using Tingle.EventBus.Transports.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for RabbitMQ.
/// </summary>
public static class EventBusBuilderExtensions
{
    /// <summary>Add RabbitMQ transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddRabbitMqTransport(this EventBusBuilder builder, Action<RabbitMqTransportOptions>? configure = null)
        => builder.AddRabbitMqTransport(TransportNames.RabbitMq, configure);

    /// <summary>Add RabbitMQ transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddRabbitMqTransport(this EventBusBuilder builder, string name, Action<RabbitMqTransportOptions>? configure = null)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.Services.ConfigureOptions<RabbitMqConfigureOptions>();
        return builder.AddTransport<RabbitMqTransportOptions, RabbitMqTransport>(name, configure);
    }
}
