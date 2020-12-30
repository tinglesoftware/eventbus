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

            // setup HostInfo
            if (options.HostInfo == null)
            {
                var entry = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetCallingAssembly();
                options.HostInfo = new HostInfo
                {
                    ApplicationName = environment.ApplicationName,
                    ApplicationVersion = entry.GetName().Version.ToString(),
                    EnvironmentName = environment.EnvironmentName,
                    LibraryVersion = typeof(IEventBus).Assembly.GetName().Version.ToString(),
                    MachineName = Environment.MachineName,
                    OperatingSystem = Environment.OSVersion.ToString(),
                };
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
