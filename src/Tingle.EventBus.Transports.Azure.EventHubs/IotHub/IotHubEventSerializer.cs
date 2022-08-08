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

    public IotHubEventSerializer(IOptionsMonitor<EventBusSerializationOptions> optionsAccessor,
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
        object? telemetry = null, twinChange = null, lifecycle = null, connectionState = null;

        var source = Enum.Parse<IotHubEventMessageSource>(data.GetIotHubMessageSource()!, ignoreCase: true);
        if (source == IotHubEventMessageSource.Telemetry)
        {
            telemetry = await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                              returnType: mapped.TelemetryType,
                                                              options: serializerOptions,
                                                              cancellationToken: cancellationToken);
        }
        else if (source is IotHubEventMessageSource.TwinChangeEvents
                        or IotHubEventMessageSource.DeviceLifecycleEvents
                        or IotHubEventMessageSource.DeviceConnectionStateEvents)
        {
            var hubName = data.GetPropertyValue<string>("hubName");
            var deviceId = data.GetPropertyValue<string>("deviceId");
            var moduleId = data.GetPropertyValue<string>("moduleId");
            var operationType = data.GetPropertyValue<string>("opType")!;
            var type = Enum.Parse<IotHubOperationalEventType>(operationType, ignoreCase: true);
            var operationTimestamp = data.GetPropertyValue<string>("operationTimestamp");

            if (source == IotHubEventMessageSource.TwinChangeEvents)
            {
                var twinChangeEvent = await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                                            returnType: mapped.TwinChangeEventType,
                                                                            options: serializerOptions,
                                                                            cancellationToken: cancellationToken);

                var twinChangeOpEventType = typeof(IotHubOperationalEvent<>).MakeGenericType(mapped.TwinChangeEventType);
                twinChange = Activator.CreateInstance(twinChangeOpEventType, new[] { hubName, deviceId, moduleId, type, operationTimestamp, twinChangeEvent, });
            }
            else if (source == IotHubEventMessageSource.DeviceLifecycleEvents)
            {
                var lifecycleEvent = await JsonSerializer.DeserializeAsync(utf8Json: stream,
                                                                           returnType: mapped.LifecycleEventType,
                                                                           options: serializerOptions,
                                                                           cancellationToken: cancellationToken);

                var lifecycleOpEventType = typeof(IotHubOperationalEvent<>).MakeGenericType(mapped.LifecycleEventType);
                lifecycle = Activator.CreateInstance(lifecycleOpEventType, new[] { hubName, deviceId, moduleId, type, operationTimestamp, lifecycleEvent, });
            }
            else if (source == IotHubEventMessageSource.DeviceConnectionStateEvents)
            {
                var connectionStateEvent = await JsonSerializer.DeserializeAsync<IotHubDeviceConnectionStateEvent>(
                    utf8Json: stream,
                    options: serializerOptions,
                    cancellationToken: cancellationToken);

                connectionState = new IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>(
                    hubName: hubName,
                    deviceId: deviceId,
                    moduleId: moduleId,
                    type: type,
                    operationTimestamp: operationTimestamp,
                    @event: connectionStateEvent!);
            }
        }

        var args = new object?[] { source, telemetry, twinChange, lifecycle, connectionState, };
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
                           lifecycleEventType: baseType.GenericTypeArguments[2]);
            }

            baseType = baseType.BaseType;
        }

        throw new InvalidOperationException("Reached the end but could not get the inner types. This should not happen. Report it.");
    }

    private record MappedTypes
    {
        public MappedTypes(Type telemetryType, Type twinChangeEventType, Type lifecycleEventType)
        {
            TelemetryType = telemetryType ?? throw new ArgumentNullException(nameof(telemetryType));
            TwinChangeEventType = twinChangeEventType ?? throw new ArgumentNullException(nameof(twinChangeEventType));
            LifecycleEventType = lifecycleEventType ?? throw new ArgumentNullException(nameof(lifecycleEventType));
        }

        public Type TelemetryType { get; }
        public Type TwinChangeEventType { get; }
        public Type LifecycleEventType { get; }
    }
}
