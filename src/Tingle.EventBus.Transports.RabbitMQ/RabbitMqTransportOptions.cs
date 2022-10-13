using RabbitMQ.Client;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Options for configuring RabbitMQ based event bus.
/// </summary>
public class RabbitMqTransportOptions : EventBusTransportOptionsBase
{
    /// <inheritdoc/>
    public override EntityKind DefaultEntityKind { get; set; } = EntityKind.Broadcast;

    /// <summary>
    /// The number of retries to make.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The host name of the broker.
    /// Defaults to <c>localhost</c>
    /// </summary>
    public string Hostname { get; set; } = "localhost";

    /// <summary>
    /// The username for authenticating on the broker.
    /// Defaults to <c>quest</c>.
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// The password for authenticating on the broker.
    /// Defaults to <c>guest</c>.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// The factory for creating <see cref="IConnection"/> instances when needed.
    /// When not provided, a factory is created from the settings available in this class.
    /// </summary>
    public ConnectionFactory? ConnectionFactory { get; set; }
}
