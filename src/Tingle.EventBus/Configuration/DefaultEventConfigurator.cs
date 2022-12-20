using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Default implementation of <see cref="IEventConfigurator"/>.
/// </summary>
internal class DefaultEventConfigurator : IEventConfigurator
{
    private readonly IHostEnvironment environment;
    private readonly IEventBusConfigurationProvider configurationProvider;

    public DefaultEventConfigurator(IHostEnvironment environment, IEventBusConfigurationProvider configurationProvider)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
    }

    /// <inheritdoc/>
    public void Configure(EventRegistration registration, EventBusOptions options)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));
        if (options is null) throw new ArgumentNullException(nameof(options));

        // bind from IConfiguration
        var configuration = configurationProvider.Configuration.GetSection($"Events:{registration.EventType.FullName}");
        configuration.Bind(registration);
        foreach (var ecr in registration.Consumers.Values)
        {
            configuration.GetSection($"Consumers:{ecr.ConsumerType.FullName}").Bind(ecr);
        }

        // set transport name
        ConfigureTransportName(registration, options);

        // set event name and kind
        ConfigureEventName(registration, options.Naming);
        ConfigureEntityKind(registration);

        // set the consumer names
        ConfigureConsumerNames(registration, options.Naming);

        // set the serializer
        ConfigureSerializer(registration);
    }

    internal void ConfigureTransportName(EventRegistration reg, EventBusOptions options)
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
        if (!options.TransportMap.ContainsKey(reg.TransportName))
        {
            throw new InvalidOperationException($"Transport '{reg.TransportName}' on event '{type.FullName}' must be registered.");
        }
    }

    internal void ConfigureEventName(EventRegistration reg, EventBusNamingOptions options)
    {
        // set the event name, if not set
        if (string.IsNullOrWhiteSpace(reg.EventName))
        {
            var type = reg.EventType;
            // prioritize the attribute if available, otherwise get the type name
            var name = type.GetCustomAttributes(false).OfType<EventNameAttribute>().SingleOrDefault()?.EventName;
            if (name == null)
            {
                var typeName = options.UseFullTypeNames ? type.FullName! : type.Name;
                typeName = options.TrimCommonSuffixes(typeName);
                name = typeName;
                name = options.ApplyNamingConvention(name);
                name = options.AppendScope(name);
                name = options.ReplaceInvalidCharacters(name);
            }
            reg.EventName = name;
        }
    }

    internal void ConfigureEntityKind(EventRegistration reg)
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

        foreach (var ecr in reg.Consumers.Values)
        {
            // set the consumer name, if not set
            if (string.IsNullOrWhiteSpace(ecr.ConsumerName))
            {
                var type = ecr.ConsumerType;
                // prioritize the attribute if available, otherwise get the type name
                var name = type.GetCustomAttributes(false).OfType<ConsumerNameAttribute>().SingleOrDefault()?.ConsumerName;
                if (name == null)
                {
                    var typeName = options.UseFullTypeNames ? type.FullName! : type.Name;
                    typeName = options.TrimCommonSuffixes(typeName);
                    name = options.ConsumerNameSource switch
                    {
                        ConsumerNameSource.TypeName => typeName,
                        ConsumerNameSource.Prefix => prefix,
                        ConsumerNameSource.PrefixAndTypeName => $"{prefix}.{typeName}",
                        _ => throw new InvalidOperationException($"'{nameof(options.ConsumerNameSource)}.{options.ConsumerNameSource}' is not supported"),
                    };
                    name = options.ApplyNamingConvention(name);
                    name = options.AppendScope(name);
                    name = options.ReplaceInvalidCharacters(name);
                }
                // Appending the EventName to the consumer name can ensure it is unique
                ecr.ConsumerName = options.SuffixConsumerName ? options.Join(name, reg.EventName) : name;
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
}
