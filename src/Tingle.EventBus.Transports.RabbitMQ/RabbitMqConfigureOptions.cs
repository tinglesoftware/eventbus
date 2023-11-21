using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="RabbitMqTransportOptions"/>.
/// </summary>
internal class RabbitMqConfigureOptions : EventBusTransportConfigureOptions<RabbitMqTransportOptions>
{
    /// <summary>
    /// Initializes a new <see cref="RabbitMqConfigureOptions"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IEventBusConfigurationProvider"/> instance.</param>
    /// <param name="configurators">A list of <see cref="IEventBusConfigurator"/> to use when configuring options.</param>
    /// <param name="busOptionsAccessor">An <see cref="IOptions{TOptions}"/> for bus configuration.</param>\
    public RabbitMqConfigureOptions(IEventBusConfigurationProvider configurationProvider, IEnumerable<IEventBusConfigurator> configurators, IOptions<EventBusOptions> busOptionsAccessor)
        : base(configurationProvider, configurators, busOptionsAccessor) { }

    /// <inheritdoc/>
    public override void PostConfigure(string? name, RabbitMqTransportOptions options)
    {
        base.PostConfigure(name, options);

        // If there are consumers for this transport, confirm the right Bus options
        var registrations = BusOptions.GetRegistrations(name!);
        if (registrations.Any(r => r.Consumers.Count > 0))
        {
            // we need full type names
            if (!BusOptions.Naming.UseFullTypeNames)
            {
                throw new NotSupportedException($"When using RabbitMQ transport '{nameof(BusOptions.Naming.UseFullTypeNames)}' must be 'true'");
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
        foreach (var reg in registrations)
        {
            // Set the values using defaults
            options.SetValuesUsingDefaults(reg, BusOptions);

            // Ensure the entity type is allowed
            options.EnsureAllowedEntityKind(reg, EntityKind.Broadcast, EntityKind.Queue);

            // Event names become Exchange names and they should not be longer than 255 characters
            if (reg.EventName!.Length > 255)
            {
                throw new InvalidOperationException($"EventName '{reg.EventName}' generated from '{reg.EventType.Name}' is too long. "
                                                   + "RabbitMQ does not allow more than 255 characters for Exchange names.");
            }

            foreach (var ecr in reg.Consumers)
            {
                // Consumer names become Queue names and they should not be longer than 255 characters
                if (ecr.ConsumerName!.Length > 255)
                {
                    throw new InvalidOperationException($"ConsumerName '{ecr.ConsumerName}' generated from '{ecr.ConsumerType.Name}' is too long. "
                                                       + "RabbitMQ does not allow more than 255 characters for Queue names.");
                }

                // This does not support dead-letter yet
                if (ecr.Deadletter)
                {
                    throw new InvalidOperationException($"ConsumerName '{ecr.ConsumerName}' is setup for dead-letter but the RabbitMQ "
                                                       + "implementation doesn't yet support it.");
                }
            }
        }
    }
}
