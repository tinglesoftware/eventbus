using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Text.RegularExpressions;
using Tingle.EventBus;
using Tingle.EventBus.Serialization;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="EventBusOptions"/>.
    /// </summary>
    internal class EventBusPostConfigureOptions : IPostConfigureOptions<EventBusOptions>
    {
        private static readonly Regex namePattern = new Regex("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);

        private readonly IHostEnvironment environment;

        public EventBusPostConfigureOptions(IHostEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public void PostConfigure(string name, EventBusOptions options)
        {
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

            // for each event registration, ensure we have everything set
            var registrations = options.GetRegistrations();
            foreach (var reg in registrations)
            {
                // set the event name, if not set
                if (string.IsNullOrWhiteSpace(reg.EventName))
                {
                    var type = reg.EventType;
                    var ename = options.UseFullTypeNames ? type.FullName : type.Name;
                    reg.EventName = ApplyNamingConvention(ename, options.NamingConvention);
                }

                // set the consumer name, if not set
                if (string.IsNullOrWhiteSpace(reg.ConsumerName))
                {
                    // for consumers, we always enforce the full type name
                    var type = reg.ConsumerType;
                    var cname = (options.UseApplicationNameInsteadOfConsumerName && !options.ForceConsumerName)
                                ? environment.ApplicationName
                                : type.FullName;
                    reg.ConsumerName = ApplyNamingConvention(cname, options.NamingConvention);
                }
            }
        }

        private static string ApplyNamingConvention(string raw, EventBusNamingConvention convention)
        {
            return convention switch
            {
                EventBusNamingConvention.KebabCase => namePattern.Replace(raw, m => "-" + m.Value).ToLowerInvariant(),
                EventBusNamingConvention.SnakeCase => namePattern.Replace(raw, m => "_" + m.Value).ToLowerInvariant(),
                _ => raw,
            };
        }
    }
}
