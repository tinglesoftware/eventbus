using Azure.Messaging.EventHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

internal class IotHubEventSerializer : AbstractEventSerializer
{
    private static readonly Type BaseType = typeof(IotHubEvent<,,>);
    private static readonly ConcurrentDictionary<Type, MappedTypes> typeMaps = new();

    public IotHubEventSerializer(IOptionsMonitor<EventBusOptions> optionsAccessor,
                                 ILoggerFactory loggerFactory)
        : base(optionsAccessor, loggerFactory) { }

    /// <inheritdoc/>
    protected override IList<string> SupportedMediaTypes => JsonContentTypes;

    /// <inheritdoc/>
    protected override async Task<IEventEnvelope<T>?> DeserializeToEnvelopeAsync<T>(Stream stream,
                                                                                    DeserializationContext context,
                                                                                    CancellationToken cancellationToken = default)
    {
        var targetType = typeof(T);
        if (!BaseType.IsAssignableFromGeneric(targetType))
        {
            throw new NotSupportedException($"Only events that inherit from '{BaseType.FullName}' are supported for deserialization.");
        }

        var mapped = typeMaps.GetOrAdd(targetType, GetTargetTypes(targetType));

        var serializerOptions = OptionsAccessor.CurrentValue.SerializerOptions;
        var data = (EventData)context.RawTransportData!;
        object? telemetry = null, twinChange = null, lifeCycle = null;

        var source = Enum.Parse<IotHubEventMessageSource>(data.GetIotHubMessageSource()!, ignoreCase: true);
        if (source == IotHubEventMessageSource.Telemetry)
        {
            telemetry = await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                              returnType: mapped.TelemetryType,
                                                              options: serializerOptions,
                                                              cancellationToken: cancellationToken);
        }
        else if (source is IotHubEventMessageSource.TwinChangeEvents or IotHubEventMessageSource.DeviceLifecycleEvents)
        {
            var hubName = data.GetPropertyValue<string>("hubName");
            var deviceId = data.GetPropertyValue<string>("deviceId");
            var moduleId = data.GetPropertyValue<string>("moduleId");
            var operationType = data.GetPropertyValue<string>("opType")!;
            var type = Enum.Parse<IotHubOperationalEventType>(operationType, ignoreCase: true);

            if (source == IotHubEventMessageSource.TwinChangeEvents)
            {
                var twinChangeEvent = await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                                            returnType: mapped.TwinChangeEventType,
                                                                            options: serializerOptions,
                                                                            cancellationToken: cancellationToken);

                var twinChangeOpEventType = typeof(IotHubOperationalEvent<>).MakeGenericType(mapped.TwinChangeEventType);
                twinChange = Activator.CreateInstance(twinChangeOpEventType, new[] { hubName, deviceId, moduleId, type, twinChangeEvent, });
            }
            else if (source == IotHubEventMessageSource.DeviceLifecycleEvents)
            {
                var lifeCycleEvent = await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                                           returnType: mapped.LifeCycleEventType,
                                                                           options: serializerOptions,
                                                                           cancellationToken: cancellationToken);

                var lifeCycleOpEventType = typeof(IotHubOperationalEvent<>).MakeGenericType(mapped.LifeCycleEventType);
                lifeCycle = Activator.CreateInstance(lifeCycleOpEventType, new[] { hubName, deviceId, moduleId, type, lifeCycleEvent, });
            }
        }

        var args = new object?[] { source, telemetry, twinChange, lifeCycle, };
        var @event = (T?)Activator.CreateInstance(targetType, args);
        return new EventEnvelope<T> { Event = @event, };
    }

    /// <inheritdoc/>
    protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                      EventEnvelope<T> envelope,
                                                      CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Serialization of IotHub events is not allowed.");
    }

    private static MappedTypes GetTargetTypes([NotNull] Type givenType)
    {
        if (givenType is null) throw new ArgumentNullException(nameof(givenType));

        var baseType = givenType.BaseType;
        while (baseType is not null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == BaseType)
            {
                return new(telemetryType: baseType.GenericTypeArguments[0],
                           twinChangeEventType: baseType.GenericTypeArguments[1],
                           lifeCycleEventType: baseType.GenericTypeArguments[2]);
            }

            baseType = baseType.BaseType;
        }

        throw new InvalidOperationException("Reached the end but could not get the inner types. This should not happen. Report it.");
    }

    private record MappedTypes
    {
        public MappedTypes(Type telemetryType, Type twinChangeEventType, Type lifeCycleEventType)
        {
            TelemetryType = telemetryType ?? throw new ArgumentNullException(nameof(telemetryType));
            TwinChangeEventType = twinChangeEventType ?? throw new ArgumentNullException(nameof(twinChangeEventType));
            LifeCycleEventType = lifeCycleEventType ?? throw new ArgumentNullException(nameof(lifeCycleEventType));
        }

        public Type TelemetryType { get; }
        public Type TwinChangeEventType { get; }
        public Type LifeCycleEventType { get; }
    }
}
