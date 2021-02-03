using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish configure non-trivial defaults of instances of <see cref="EventBusOptions"/>.
    /// </summary>
    internal class EventBusConfigureOptions : IConfigureOptions<EventBusOptions>
    {
        private readonly IHostEnvironment environment;

        public EventBusConfigureOptions(IHostEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <inheritdoc/>
        public void Configure(EventBusOptions options)
        {
            // Set the default ConsumerNamePrefix
            options.ConsumerNamePrefix ??= environment.ApplicationName;
        }
    }
}
