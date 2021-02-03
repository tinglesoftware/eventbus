using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Extension methods on <see cref="EventRegistration"/> and <see cref="EventConsumerRegistration"/>.
    /// </summary>
    public static class RegistrationExtensions
    {
        private static readonly Regex namePattern = new Regex("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);
        private static readonly Regex replacePattern = new Regex("[^a-zA-Z0-9-_]", RegexOptions.Compiled);
        private static readonly Regex trimPattern = new Regex("(Event|Consumer|EventConsumer)$", RegexOptions.Compiled);

        internal static EventRegistration SetEventName(this EventRegistration reg, EventBusOptions options)
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
                    var typeName = options.UseFullTypeNames ? type.FullName : type.Name;
                    typeName = options.TrimCommonSuffixes(typeName);
                    ename = typeName;
                    ename = ApplyNamingConvention(ename, options.NamingConvention);
                    ename = AppendScope(ename, options.NamingConvention, options.Scope);
                    ename = ReplaceInvalidCharacters(ename, options.NamingConvention);
                }
                reg.EventName = ename;
            }

            return reg;
        }

        internal static EventRegistration SetConsumerNames(this EventRegistration reg,
                                                           EventBusOptions options,
                                                           IHostEnvironment environment)
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (environment is null) throw new ArgumentNullException(nameof(environment));

            // ensure we have the event name set
            if (string.IsNullOrWhiteSpace(reg.EventName))
            {
                throw new InvalidOperationException($"The {nameof(reg.EventName)} for must be set before setting names of the consumer.");
            }

            // prefix is either the one provided or the application name
            var prefix = options.ConsumerNamePrefix ?? environment.ApplicationName;

            foreach (var creg in reg.Consumers)
            {
                // set the consumer name, if not set
                if (string.IsNullOrWhiteSpace(creg.ConsumerName))
                {
                    var type = creg.ConsumerType;
                    // prioritize the attribute if available, otherwise get the type name
                    var cname = type.GetCustomAttributes(false).OfType<ConsumerNameAttribute>().SingleOrDefault()?.ConsumerName;
                    if (cname == null)
                    {
                        var typeName = options.UseFullTypeNames ? type.FullName : type.Name;
                        typeName = options.TrimCommonSuffixes(typeName);
                        cname = options.ConsumerNameSource switch
                        {
                            ConsumerNameSource.TypeName => typeName,
                            ConsumerNameSource.Prefix => prefix,
                            ConsumerNameSource.PrefixAndTypeName => $"{prefix}.{typeName}",
                            _ => throw new InvalidOperationException($"'{nameof(options.ConsumerNameSource)}.{options.ConsumerNameSource}' is not supported"),
                        };
                        cname = ApplyNamingConvention(cname, options.NamingConvention);
                        cname = AppendScope(cname, options.NamingConvention, options.Scope);
                        cname = ReplaceInvalidCharacters(cname, options.NamingConvention);
                    }
                    // Append EventName to ensure consumer name is unique
                    creg.ConsumerName = Join(options.NamingConvention, cname, reg.EventName);
                }
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

        internal static string TrimCommonSuffixes(this EventBusOptions options, string untrimmed)
        {
            return options.TrimTypeNames ? trimPattern.Replace(untrimmed, "") : untrimmed;
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
