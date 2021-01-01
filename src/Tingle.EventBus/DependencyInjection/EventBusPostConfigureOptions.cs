using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus;
using Tingle.EventBus.Registrations;
using Tingle.EventBus.Serialization;

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

        public void PostConfigure(string name, EventBusOptions options)
        {
            // check bounds for startup delay, if provided
            if (options.StartupDelay != null)
            {
                var ticks = options.StartupDelay.Value.Ticks;
                ticks = Math.Max(ticks, TimeSpan.FromSeconds(5).Ticks); // must be more than 5 seconds
                ticks = Math.Min(ticks, TimeSpan.FromMinutes(10).Ticks); // must be less than 10 minutes
                options.StartupDelay = TimeSpan.FromTicks(ticks);
            }

            // check bounds for duplicate detection duration, if duplicate detection is enabled
            if (options.EnableDeduplication)
            {
                var ticks = options.DuplicateDetectionDuration.Ticks;
                ticks = Math.Max(ticks, TimeSpan.FromSeconds(20).Ticks); // must be more than 20 seconds
                ticks = Math.Min(ticks, TimeSpan.FromDays(7).Ticks); // must be less than 7 days
                options.DuplicateDetectionDuration = TimeSpan.FromTicks(ticks);
            }

            // setup HostInfo
            if (options.HostInfo == null)
            {
                var entry = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetCallingAssembly();
                options.HostInfo = new HostInfo
                {
                    ApplicationName = environment.ApplicationName,
                    ApplicationVersion = entry.GetName().Version.ToString(),
                    EnvironmentName = environment.EnvironmentName,
                    LibraryVersion = typeof(EventBus).Assembly.GetName().Version.ToString(),
                    MachineName = Environment.MachineName,
                    OperatingSystem = Environment.OSVersion.ToString(),
                };
            }

            // ensure there is at least one registered transport
            if (options.RegisteredTransportNames.Count == 0)
            {
                throw new InvalidOperationException("There must be at least one registered transport");
            }

            // if the default transport name has been set, ensure it is registered
            if (!string.IsNullOrWhiteSpace(options.DefaultTransportName))
            {
                // ensure the transport name set has been registered
                var tname = options.DefaultTransportName;
                if (!options.RegisteredTransportNames.ContainsKey(tname))
                {
                    throw new InvalidOperationException($"The default transport  specified '{tname}' must be a registered one.");
                }
            }

            // if the default transport name has not been set, and there is one registered, set it as default
            if (string.IsNullOrWhiteSpace(options.DefaultTransportName))
            {
                if (options.RegisteredTransportNames.Count == 1)
                {
                    options.DefaultTransportName = options.RegisteredTransportNames.Single().Key;
                }
            }

            // for each consumer registration, ensure we have everything set
            var registrations = options.GetConsumerRegistrations();
            foreach (var reg in registrations)
            {
                reg.SetSerializer() // set the serializer
                   .SetEventName(options) // set the event name
                   .SetTransportName(options) // set the transport name
                   .SetConsumerName(options, environment); // set the consumer name
            }

            // ensure there are no events with the same name
            var grouped = registrations.GroupBy(r => r.EventName);
            var conflicted = grouped.FirstOrDefault(kvp => kvp.Count() > 1);
            if (conflicted != null)
            {
                throw new InvalidOperationException($"The event name '{conflicted.Key}' cannot be used more than once."
                                                  + $" Types: - {string.Join("\r\n- ", conflicted.Select(r => r.EventType.FullName))}");
            }

            // ensure there are no consumers with the same name
            grouped = registrations.GroupBy(r => r.ConsumerName);
            conflicted = grouped.FirstOrDefault(kvp => kvp.Count() > 1);
            if (conflicted != null)
            {
                throw new InvalidOperationException($"The consumer name '{conflicted.Key}' cannot be used more than once."
                                                  + $" Types: - {string.Join("\r\n- ", conflicted.Select(r => r.ConsumerType.FullName))}");
            }
        }
    }
}
