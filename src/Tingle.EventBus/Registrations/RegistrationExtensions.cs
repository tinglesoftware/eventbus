using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Registrations
{
    internal static class RegistrationExtensions
    {
        private static readonly Regex namePattern = new Regex("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);

        internal static ConsumerRegistration SetConsumerName(this ConsumerRegistration reg,
                                                             EventBusOptions options,
                                                             IHostEnvironment environment)
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (environment is null) throw new ArgumentNullException(nameof(environment));

            // set the consumer name, if not set
            if (string.IsNullOrWhiteSpace(reg.ConsumerName))
            {
                var type = reg.ConsumerType;
                // prioritize the attribute if available, otherwise get the type name
                var cname = type.GetCustomAttributes(false).OfType<ConsumerNameAttribute>().SingleOrDefault()?.ConsumerName;
                if (cname == null)
                {
                    // for consumers, we always enforce the full type name
                    cname = (options.UseApplicationNameInsteadOfConsumerName && !options.ForceConsumerName)
                            ? environment.ApplicationName
                            : type.FullName;
                    cname = ApplyNamingConvention(cname, options.NamingConvention);
                    cname = AppendScope(cname, options.NamingConvention, options.Scope);
                    cname = ReplaceInvalidCharacters(cname, options.NamingConvention);
                }
                reg.ConsumerName = cname;
            }

            return reg;
        }

        internal static T SetEventName<T>(this T reg, EventBusOptions options) where T : EventRegistration
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));
            if (options is null) throw new ArgumentNullException(nameof(options));

            // set the event name, if not set
            if (string.IsNullOrWhiteSpace(reg.EventName))
            {
                var type = reg.EventType;
                // prioritize the attribute if available, otherwise get the type name
                var ename = type.GetCustomAttributes(false).OfType<EventNameAttribute>().SingleOrDefault()?.EventName;
                if (ename == null)
                {
                    ename = options.UseFullTypeNames ? type.FullName : type.Name;
                    ename = ApplyNamingConvention(ename, options.NamingConvention);
                    ename = AppendScope(ename, options.NamingConvention, options.Scope);
                    ename = ReplaceInvalidCharacters(ename, options.NamingConvention);
                }
                reg.EventName = ename;
            }

            return reg;
        }

        internal static T SetSerializer<T>(this T reg) where T : EventRegistration
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));

            // if the event serializer has not been specified, attempt to get from the attribute
            var attrs = reg.EventType.GetCustomAttributes(false);
            reg.EventSerializerType ??= attrs.OfType<EventSerializerAttribute>().SingleOrDefault()?.SerializerType;
            reg.EventSerializerType ??= typeof(IEventSerializer); // use the default when not provided

            // ensure the serializer is either default or it implements IEventSerializer
            if (reg.EventSerializerType != typeof(IEventSerializer)
                && !typeof(IEventSerializer).IsAssignableFrom(reg.EventSerializerType))
            {
                throw new InvalidOperationException($"The type '{reg.EventSerializerType.FullName}' is used as a serializer "
                                                  + $"but does not implement '{typeof(IEventSerializer).FullName}'");
            }

            return reg;
        }

        internal static T SetTransportName<T>(this T reg, EventBusOptions options) where T : EventRegistration
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));
            if (options is null) throw new ArgumentNullException(nameof(options));

            var type = reg.EventType;

            // if the event transport name has not been specified, attempt to get from the attribute
            reg.TransportName ??= type.GetCustomAttributes(false).OfType<EventTransportNameAttribute>().SingleOrDefault()?.Name;

            // if the event transport name has not been set, try the default one
            reg.TransportName ??= options.DefaultTransportName;

            // set the transport name from the default, if not set
            if (string.IsNullOrWhiteSpace(reg.TransportName))
            {
                throw new InvalidOperationException($"Unable to set the transport for event '{type.FullName}'."
                                                  + $" Either set the '{nameof(options.DefaultTransportName)}' option"
                                                  + $" or use the '{typeof(EventTransportNameAttribute).FullName}' on the event.");
            }

            // ensure the transport name set has been registered
            if (!options.RegisteredTransportNames.ContainsKey(reg.TransportName))
            {
                throw new InvalidOperationException($"Transport '{reg.TransportName}' on event '{type.FullName}' must be registered.");
            }

            return reg;
        }

        internal static string GetApplicationName(this EventBusOptions options, IHostEnvironment environment)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (environment is null) throw new ArgumentNullException(nameof(environment));

            var name = environment.ApplicationName;
            name = ApplyNamingConvention(name, options.NamingConvention);
            name = AppendScope(name, options.NamingConvention, options.Scope);
            name = ReplaceInvalidCharacters(name, options.NamingConvention);
            return name;
        }

        internal static string ApplyNamingConvention(string raw, NamingConvention convention)
        {
            return convention switch
            {
                NamingConvention.KebabCase => namePattern.Replace(raw, m => "-" + m.Value).ToLowerInvariant(),
                NamingConvention.SnakeCase => namePattern.Replace(raw, m => "_" + m.Value).ToLowerInvariant(),
                _ => raw,
            };
        }

        internal static string ReplaceInvalidCharacters(string raw, NamingConvention convention)
        {
            return convention switch
            {
                NamingConvention.KebabCase => Regex.Replace(raw, "[^a-z0-9-]", "-"),
                NamingConvention.SnakeCase => Regex.Replace(raw, "[^a-z0-9-]", "_"),
                _ => Regex.Replace(raw, "[^a-zA-Z0-9-_]", ""),
            };
        }

        internal static string AppendScope(string unscoped, NamingConvention convention, string scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) return unscoped;

            return convention switch
            {
                NamingConvention.KebabCase => string.Join("-", scope, unscoped).ToLowerInvariant(),
                NamingConvention.SnakeCase => string.Join("_", scope, unscoped).ToLowerInvariant(),
                _ => throw new ArgumentOutOfRangeException(nameof(convention), $"'{convention}' does not support scoping"),
            };
        }
    }
}
