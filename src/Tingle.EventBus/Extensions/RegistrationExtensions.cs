using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Extension methods on <see cref="EventRegistration"/> and <see cref="ConsumerRegistration"/>.
    /// </summary>
    public static class RegistrationExtensions
    {
        private static readonly Regex namePattern = new Regex("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);
        private static readonly Regex replacePattern = new Regex("[^a-zA-Z0-9-_]", RegexOptions.Compiled);

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
                    // for consumerstype name source, we always use the full type name
                    cname = options.ConsumerNameSource switch
                    {
                        ConsumerNameSource.TypeName => type.FullName,
                        ConsumerNameSource.ApplicationName => environment.ApplicationName,
                        _ => throw new InvalidOperationException($"'{nameof(options.ConsumerNameSource)}.{options.ConsumerNameSource}' is not supported"),
                    };
                    cname = ApplyNamingConvention(cname, options.NamingConvention);
                    cname = AppendScope(cname, options.NamingConvention, options.Scope);
                    cname = ReplaceInvalidCharacters(cname, options.NamingConvention);
                }
                // Append EventName to ensure consumer name is unique
                reg.ConsumerName = Join(options.NamingConvention, cname, reg.EventName);
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
                NamingConvention.KebabCase => replacePattern.Replace(raw, "-"),
                NamingConvention.SnakeCase => replacePattern.Replace(raw, "_"),
                _ => replacePattern.Replace(raw, ""),
            };
        }

        internal static string AppendScope(string unscoped, NamingConvention convention, string scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) return unscoped;
            return Join(convention, scope, unscoped);
        }

        internal static string Join(NamingConvention convention, params string[] args)
        {
            if (args is null) throw new ArgumentNullException(nameof(args));

            // remove nulls
            args = args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();

            return convention switch
            {
                NamingConvention.KebabCase => string.Join("-", args).ToLowerInvariant(),
                NamingConvention.SnakeCase => string.Join("_", args).ToLowerInvariant(),
                _ => throw new ArgumentOutOfRangeException(nameof(convention), $"'{convention}' does not support joining"),
            };
        }
    }
}
