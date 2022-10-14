using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.RabbitMQ;

/// <summary>Defaults for <see cref="RabbitMqTransportOptions"/>.</summary>
public static class RabbitMqDefaults
{
    /// <summary>Default name for <see cref="RabbitMqTransportOptions"/>.</summary>
    public const string Name = "rabbitmq";
}
