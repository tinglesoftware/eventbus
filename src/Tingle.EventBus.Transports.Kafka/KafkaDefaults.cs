using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Transports.Kafka;

/// <summary>Defaults for <see cref="KafkaTransportOptions"/>.</summary>
public static class KafkaDefaults
{
    /// <summary>Default name for <see cref="KafkaTransportOptions"/>.</summary>
    public const string Name = "kafka";
}
