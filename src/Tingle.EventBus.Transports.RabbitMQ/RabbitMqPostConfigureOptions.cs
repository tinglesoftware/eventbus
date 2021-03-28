using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;
using System.Linq;
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
            // If there are consumers for this transport, confirm the right Bus options
            var registrations = busOptions.GetRegistrations(TransportNames.RabbitMq);
            if (registrations.Any(r => r.Consumers.Count > 0))
            {
                // we need full type names
                if (!busOptions.Naming.UseFullTypeNames)
                {
                    throw new NotSupportedException($"When using RabbitMQ transport '{nameof(busOptions.Naming.UseFullTypeNames)}' must be 'true'");
                }

                // consumer names must be suffixed
                if (!busOptions.Naming.SuffixConsumerName)
                {
                    throw new NotSupportedException($"When using RabbitMQ transport '{nameof(busOptions.Naming.SuffixConsumerName)}' must be 'true'");
                }
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

            // Ensure the entity names are not longer than the limits
            // See https://www.rabbitmq.com/queues.html#:~:text=Names,bytes%20of%20UTF%2D8%20characters.
            foreach (var ereg in registrations)
            {
                // Ensure the entity type is allowed
                options.EnsureAllowedEntityKind(ereg, EntityTypePreference.Topic, EntityTypePreference.Queue);

                // Event names become Exchange names and they should not be longer than 255 characters
                if (ereg.EventName.Length > 255)
                {
                    throw new InvalidOperationException($"EventName '{ereg.EventName}' generated from '{ereg.EventType.Name}' is too long. "
                                                       + "RabbitMQ does not allow more than 255 characters for Exchange names.");
                }

                // Consumer names become Queue names and they should not be longer than 255 characters
                foreach (var creg in ereg.Consumers)
                {
                    if (creg.ConsumerName.Length > 255)
                    {
                        throw new InvalidOperationException($"ConsumerName '{creg.ConsumerName}' generated from '{creg.ConsumerType.Name}' is too long. "
                                                           + "RabbitMQ does not allow more than 255 characters for Queue names.");
                    }
                }
            }
        }
    }
}
