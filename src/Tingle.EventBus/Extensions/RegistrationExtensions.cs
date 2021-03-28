using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Extension methods on <see cref="EventRegistration"/> and <see cref="EventConsumerRegistration"/>.
    /// </summary>
    public static class RegistrationExtensions
    {
        internal static EventRegistration SetEventName(this EventRegistration reg, EventBusNamingOptions options)
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
                    ename = options.ApplyNamingConvention(ename);
                    ename = options.AppendScope(ename);
                    ename = options.ReplaceInvalidCharacters(ename);
                }
                reg.EventName = ename;
            }

            return reg;
        }

        internal static EventRegistration SetEntityKind(this EventRegistration reg)
        {
            if (reg is null) throw new ArgumentNullException(nameof(reg));

            // set the entity kind, if not set and there is an attribute
            if (reg.EntityKind == null)
            {
                var type = reg.EventType;
                var kind = type.GetCustomAttributes(false).OfType<EntityKindAttribute>().SingleOrDefault()?.Kind;
                if (kind != null)
                {
                    reg.EntityKind = kind;
                }
            }

            return reg;
        }

        internal static EventRegistration SetConsumerNames(this EventRegistration reg,
                                                           EventBusNamingOptions options,
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
                        cname = options.ApplyNamingConvention(cname);
                        cname = options.AppendScope(cname);
                        cname = options.ReplaceInvalidCharacters(cname);
                    }
                    // Appending the EventName to the consumer name can ensure it is unique
                    creg.ConsumerName = options.SuffixConsumerName ? options.Join(cname, reg.EventName) : cname;
                }
            }

            return reg;
        }
    }
}
