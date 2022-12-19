using Tingle.EventBus.Transports.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="EventBusBuilder"/> for RabbitMQ.
/// </summary>
public static class RabbitMqEventBusBuilderExtensions
{
    /// <summary>Add RabbitMQ transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddRabbitMqTransport(this EventBusBuilder builder, Action<RabbitMqTransportOptions>? configure = null)
        => builder.AddRabbitMqTransport(RabbitMqDefaults.Name, configure);

    /// <summary>Add RabbitMQ transport.</summary>
    /// <param name="builder">The <see cref="EventBusBuilder"/> to add to.</param>
    /// <param name="name">The name of the transport</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the transport options.</param>
    /// <returns></returns>
    public static EventBusBuilder AddRabbitMqTransport(this EventBusBuilder builder, string name, Action<RabbitMqTransportOptions>? configure = null)
        => builder.AddTransport<RabbitMqTransport, RabbitMqTransportOptions, RabbitMqConfigureOptions>(name, configure);
}
