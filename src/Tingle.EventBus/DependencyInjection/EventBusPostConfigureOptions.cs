using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using Tingle.EventBus.Registrations;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="EventBusOptions"/>.
    /// </summary>
    internal class EventBusPostConfigureOptions : IPostConfigureOptions<EventBusOptions>
    {
        private readonly IHostEnvironment environment;

        public EventBusPostConfigureOptions(IHostEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <inheritdoc/>
        public void PostConfigure(string name, EventBusOptions options)
        {
            // Check bounds for readiness timeout
            var ticks = options.Readiness.Timeout.Ticks;
            if (options.Readiness.Enabled)
            {
                ticks = Math.Max(ticks, TimeSpan.FromSeconds(5).Ticks); // must be more than 5 seconds
                ticks = Math.Min(ticks, TimeSpan.FromMinutes(15).Ticks); // must be less than 15 minutes
                options.Readiness.Timeout = TimeSpan.FromTicks(ticks);
            }

            // Check bounds for startup delay, if provided
            if (options.StartupDelay != null)
            {
                ticks = options.StartupDelay.Value.Ticks;
                ticks = Math.Max(ticks, TimeSpan.FromSeconds(5).Ticks); // must be more than 5 seconds
                ticks = Math.Min(ticks, TimeSpan.FromMinutes(10).Ticks); // must be less than 10 minutes
                options.StartupDelay = TimeSpan.FromTicks(ticks);
            }

            // Check bounds for duplicate detection duration, if duplicate detection is enabled
            if (options.EnableDeduplication)
            {
                ticks = options.DuplicateDetectionDuration.Ticks;
                ticks = Math.Max(ticks, TimeSpan.FromSeconds(20).Ticks); // must be more than 20 seconds
                ticks = Math.Min(ticks, TimeSpan.FromDays(7).Ticks); // must be less than 7 days
                options.DuplicateDetectionDuration = TimeSpan.FromTicks(ticks);
            }

            // Ensure we have HostInfo set
            if (options.HostInfo == null)
            {
                throw new InvalidOperationException($"'{nameof(options.HostInfo)}' must be set.");
            }

            // Ensure there is at least one registered transport
            if (options.RegisteredTransportNames.Count == 0)
            {
                throw new InvalidOperationException("There must be at least one registered transport.");
            }

            // If the default transport name has been set, ensure it is registered
            if (!string.IsNullOrWhiteSpace(options.DefaultTransportName))
            {
                // ensure the transport name set has been registered
                var tname = options.DefaultTransportName;
                if (!options.RegisteredTransportNames.ContainsKey(tname))
                {
                    throw new InvalidOperationException($"The default transport  specified '{tname}' must be a registered one.");
                }
            }

            // If the default transport name has not been set, and there is only one registered, set it as default
            if (string.IsNullOrWhiteSpace(options.DefaultTransportName))
            {
                if (options.RegisteredTransportNames.Count == 1)
                {
                    options.DefaultTransportName = options.RegisteredTransportNames.Single().Key;
                }
            }

            // Setup names and transports for each event and its consumers
            var registrations = options.Registrations.Values.ToList();
            foreach (var evr in registrations)
            {
                evr.SetSerializer() // set serializer
                   .SetTransportName(options) // set transport name
                   .SetEventName(options.Naming) // set event name
                   .SetEntityKind()
                   .SetConsumerNames(options.Naming, environment) // set the consumer names
                   .SetReadinessProviders(); // set the readiness provider
            }

            // Ensure there are no events with the same name
            var conflicted = registrations.GroupBy(r => r.EventName).FirstOrDefault(kvp => kvp.Count() > 1);
            if (conflicted != null)
            {
                var names = conflicted.Select(r => r.EventType.FullName);
                throw new InvalidOperationException($"The event name '{conflicted.Key}' cannot be used more than once."
                                                  + $" Types:\r\n- {string.Join("\r\n- ", names)}");
            }

            // Ensure there are no consumers with the same name per event
            foreach (var evr in registrations)
            {
                var conflict = evr.Consumers.GroupBy(ecr => ecr.ConsumerName).FirstOrDefault(kvp => kvp.Count() > 1);
                if (conflict != null)
                {
                    var names = conflict.Select(r => r.ConsumerType.FullName);
                    throw new InvalidOperationException($"The consumer name '{conflict.Key}' cannot be used more than once on '{evr.EventType.Name}'."
                                                      + $" Types:\r\n- {string.Join("\r\n- ", names)}");
                }
            }
        }
    }
}
