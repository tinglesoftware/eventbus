using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="RabbitMqTransportOptions"/>.
    /// </summary>
    internal class RabbitMqPostConfigureOptions : IPostConfigureOptions<RabbitMqTransportOptions>
    {
        private readonly EventBusOptions busOptions;

        public RabbitMqPostConfigureOptions(IOptions<EventBusOptions> busOptionsAccessor)
        {
            busOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        }

        public void PostConfigure(string name, RabbitMqTransportOptions options)
        {
            // If there are consumers for this transport, they can only use TypeName source
            var consumers = busOptions.GetConsumerRegistrations(TransportNames.RabbitMq);
            if (consumers.Count > 0 && busOptions.ConsumerNameSource != ConsumerNameSource.TypeName)
            {
                throw new NotSupportedException($"When using RabbitMQ transport '{nameof(busOptions.ConsumerNameSource)}' must be '{ConsumerNameSource.TypeName}'");
            }

            // if we do not have a connection factory, attempt to create one
            if (options.ConnectionFactory == null)
            {
                // ensure we have a hostname
                if (string.IsNullOrWhiteSpace(options.Hostname))
                {
                    throw new ArgumentNullException(nameof(options.Hostname), "The hostname is required to connect to a RabbitMQ broker");
                }

                // ensure we have a username and password
                options.Username ??= "guest";
                options.Password ??= "guest";

                options.ConnectionFactory = new ConnectionFactory
                {
                    HostName = options.Hostname,
                    UserName = options.Username,
                    Password = options.Password,
                };
            }

            // at this point we have a connection factory, ensure certain settings are what we need them to be
            options.ConnectionFactory.DispatchConsumersAsync = true;

            // ensure the retries are not less than zero
            options.RetryCount = Math.Max(options.RetryCount, 0);
        }
    }
}
