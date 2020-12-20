using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus
{
    internal static class RegistrationExtensions
    {
        private static readonly Regex namePattern = new Regex("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);

        internal static ConsumerRegistration SetConsumerName(this ConsumerRegistration reg,
                                                             EventBusOptions options,
                                                             IHostEnvironment environment)
        {
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
                    cname = AppendScope(cname, options.NamingConvention, options.Scope);
                    cname = ReplaceInvalidCharacters(cname, options.NamingConvention);
                }
                reg.ConsumerName = cname;
            }

            return reg;
        }

        internal static T SetEventName<T>(this T reg, EventBusOptions options) where T : EventRegistration
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
                    ename = AppendScope(ename, options.NamingConvention, options.Scope);
                    ename = ReplaceInvalidCharacters(ename, options.NamingConvention);
                }
                reg.EventName = ename;
            }

            return reg;
        }

        internal static T SetSerializer<T>(this T reg) where T : EventRegistration
        {
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

            return reg;
        }


        internal static string ApplyNamingConvention(string raw, EventBusNamingConvention convention)
        {
            return convention switch
            {
                EventBusNamingConvention.KebabCase => namePattern.Replace(raw, m => "-" + m.Value).ToLowerInvariant(),
                EventBusNamingConvention.SnakeCase => namePattern.Replace(raw, m => "_" + m.Value).ToLowerInvariant(),
                _ => raw,
            };
        }

        internal static string ReplaceInvalidCharacters(string raw, EventBusNamingConvention convention)
        {
            return convention switch
            {
                EventBusNamingConvention.KebabCase => Regex.Replace(raw, "[^a-z0-9-]", "-"),
                EventBusNamingConvention.SnakeCase => Regex.Replace(raw, "[^a-z0-9-]", "_"),
                _ => Regex.Replace(raw, "[^a-zA-Z0-9-_]", ""),
            };
        }

        internal static string AppendScope(string unscoped, EventBusNamingConvention convention, string scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) return unscoped;

            return convention switch
            {
                EventBusNamingConvention.KebabCase => string.Join("-", scope, unscoped).ToLowerInvariant(),
                EventBusNamingConvention.SnakeCase => string.Join("_", scope, unscoped).ToLowerInvariant(),
                _ => throw new ArgumentOutOfRangeException(nameof(convention), $"'{convention}' does not support scoping"),
            };
        }
    }
}
