using RabbitMQ.Client;
using Tingle.EventBus;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring RabbitMQ based event bus.
    /// </summary>
    public class RabbitMqTransportOptions : EventBusTransportOptionsBase
    {
        /// <inheritdoc/>
        public override EntityTypePreference DefaultEntityType { get; set; } = EntityTypePreference.Topic;

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
        /// The username for authenenticating on the broker.
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
        /// When not provided, aa factory is created from the settings available in this class.
        /// </summary>
        public ConnectionFactory ConnectionFactory { get; set; }
    }
}
