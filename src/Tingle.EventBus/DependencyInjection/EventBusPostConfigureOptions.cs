using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
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
                    // prioritize the attribute if available, otherwise get the type name
                    var ename = type.CustomAttributes.OfType<EventNameAttribute>().SingleOrDefault()?.EventName;
                    if (ename == null)
                    {
                        ename = options.UseFullTypeNames ? type.FullName : type.Name;
                        ename = ApplyNamingConvention(ename, options.NamingConvention);
                        ename = AppendScope(ename, options);
                        ename = ReplaceInvalidCharacters(ename, options.NamingConvention);
                    }
                    reg.EventName = ename;
                }

                // set the consumer name, if not set
                if (string.IsNullOrWhiteSpace(reg.ConsumerName))
                {
                    var type = reg.ConsumerType;
                    // prioritize the attribute if available, otherwise get the type name
                    var cname = type.CustomAttributes.OfType<ConsumerNameAttribute>().SingleOrDefault()?.ConsumerName;
                    if (cname == null)
                    {
                        // for consumers, we always enforce the full type name
                        cname = (options.UseApplicationNameInsteadOfConsumerName && !options.ForceConsumerName)
                                ? environment.ApplicationName
                                : type.FullName;
                        cname = ApplyNamingConvention(cname, options.NamingConvention);
                        cname = AppendScope(cname, options);
                        cname = ReplaceInvalidCharacters(cname, options.NamingConvention);
                    }
                    reg.ConsumerName = cname;
                }

                // if the event serializer has not been specified, attempt to get from the attribute
                reg.EventSerializerType ??= reg.EventType.CustomAttributes.OfType<EventSerializerAttribute>().SingleOrDefault()?.SerializerType;
                reg.EventSerializerType ??= typeof(IEventSerializer); // use the default when not provided

                // ensure the serializer is either default or it implements IEventSerializer
                if (reg.EventSerializerType != typeof(IEventSerializer)
                    && !typeof(IEventSerializer).IsAssignableFrom(reg.EventSerializerType))
                {
                    throw new InvalidOperationException($"The type '{reg.EventSerializerType.FullName}' is used as a serializer "
                                                      + $"but does not implement '{typeof(IEventSerializer).FullName}'");
                }
            }
        }

        private string AppendScope(string unscoped, EventBusOptions options)
        {
            var scope = options.Scope;
            if (string.IsNullOrWhiteSpace(scope)) return unscoped;

            return options.NamingConvention switch
            {
                EventBusNamingConvention.KebabCase => string.Join("-", scope, unscoped).ToLowerInvariant(),
                EventBusNamingConvention.SnakeCase => string.Join("_", scope, unscoped).ToLowerInvariant(),
                _ => throw new ArgumentOutOfRangeException(nameof(options.NamingConvention), $"'{options.NamingConvention}' does not support scoping"),
            };
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

        private static string ReplaceInvalidCharacters(string raw, EventBusNamingConvention convention)
        {
            return convention switch
            {
                EventBusNamingConvention.KebabCase => Regex.Replace(raw, "[^a-z0-9-]", "-"),
                EventBusNamingConvention.SnakeCase => Regex.Replace(raw, "[^a-z0-9-]", "_"),
                _ => Regex.Replace(raw, "[^a-zA-Z0-9-]", ""),
            };
        }
    }
}
