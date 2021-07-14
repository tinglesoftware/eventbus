using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using Tingle.EventBus.Readiness;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Registrations
{
    /// <summary>
    /// Default implementation of <see cref="IEventConfigurator"/>.
    /// </summary>
    internal class DefaultEventConfigurator : IEventConfigurator
    {
        private readonly IHostEnvironment environment;

        public DefaultEventConfigurator(IHostEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <inheritdoc/>
        public void Configure(EventRegistration registration, EventBusOptions options)
        {
            if (registration is null) throw new ArgumentNullException(nameof(registration));
            if (options is null) throw new ArgumentNullException(nameof(options));

            // set transport name
            SetTransportName(registration, options);

            // set event name and kind
            SetEventName(registration, options.Naming);
            SetEntityKind(registration);

            // set the consumer names
            ConfigureConsumerNames(registration, options.Naming);

            // set the serializer and the readiness provider
            ConfigureSerializer(registration);
            ConfigureReadinessProviders(registration);
        }

        internal void SetTransportName(EventRegistration reg, EventBusOptions options)
        {
            // If the event transport name has not been specified, attempt to get from the attribute
            var type = reg.EventType;
            reg.TransportName ??= type.GetCustomAttributes(false).OfType<EventTransportNameAttribute>().SingleOrDefault()?.Name;

            // If the event transport name has not been set, try the default one
            reg.TransportName ??= options.DefaultTransportName;

            // Set the transport name from the default, if not set
            if (string.IsNullOrWhiteSpace(reg.TransportName))
            {
                throw new InvalidOperationException($"Unable to set the transport for event '{type.FullName}'."
                                                  + $" Either set the '{nameof(options.DefaultTransportName)}' option"
                                                  + $" or use the '{typeof(EventTransportNameAttribute).FullName}' on the event.");
            }

            // Ensure the transport name set has been registered
            if (!options.RegisteredTransportNames.ContainsKey(reg.TransportName))
            {
                throw new InvalidOperationException($"Transport '{reg.TransportName}' on event '{type.FullName}' must be registered.");
            }
        }

        internal void SetEventName(EventRegistration reg, EventBusNamingOptions options)
        {
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
        }

        internal void SetEntityKind(EventRegistration reg)
        {
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
        }

        internal void ConfigureConsumerNames(EventRegistration reg, EventBusNamingOptions options)
        {
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
        }

        internal void ConfigureSerializer(EventRegistration reg)
        {
            // If the event serializer has not been specified, attempt to get from the attribute
            var attrs = reg.EventType.GetCustomAttributes(false);
            reg.EventSerializerType ??= attrs.OfType<EventSerializerAttribute>().SingleOrDefault()?.SerializerType;
            reg.EventSerializerType ??= typeof(IEventSerializer); // use the default when not provided

            // Ensure the serializer is either default or it implements IEventSerializer
            if (reg.EventSerializerType != typeof(IEventSerializer)
                && !typeof(IEventSerializer).IsAssignableFrom(reg.EventSerializerType))
            {
                throw new InvalidOperationException($"The type '{reg.EventSerializerType.FullName}' is used as a serializer "
                                                  + $"but does not implement '{typeof(IEventSerializer).FullName}'");
            }
        }

        internal void ConfigureReadinessProviders(EventRegistration reg)
        {
            foreach (var creg in reg.Consumers)
            {
                // If the readiness provider has not been specified, attempt to get from the attribute
                var type = creg.ConsumerType;
                var attrs = type.GetCustomAttributes(false);
                creg.ReadinessProviderType ??= attrs.OfType<ConsumerReadinessProviderAttribute>().SingleOrDefault()?.ReadinessProviderType;
                creg.ReadinessProviderType ??= typeof(IReadinessProvider); // use the default when not provided

                // Ensure the provider is either default or it implements IReadinessProvider
                if (creg.ReadinessProviderType != typeof(IReadinessProvider)
                    && !typeof(IReadinessProvider).IsAssignableFrom(creg.ReadinessProviderType))
                {
                    throw new InvalidOperationException($"The type '{creg.ReadinessProviderType.FullName}' is used as a readiness provider "
                                                      + $"but does not implement '{typeof(IReadinessProvider).FullName}'");
                }
            }
        }
    }
}
