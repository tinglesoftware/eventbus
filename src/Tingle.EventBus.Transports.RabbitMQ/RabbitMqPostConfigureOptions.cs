using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class RabbitMqPostConfigureOptions : IPostConfigureOptions<RabbitMqTransportOptions>
    {
        public void PostConfigure(string name, RabbitMqTransportOptions options)
        {
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
