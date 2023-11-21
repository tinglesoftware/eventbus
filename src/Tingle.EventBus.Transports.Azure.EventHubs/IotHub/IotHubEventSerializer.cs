using Azure.Messaging.EventHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tingle.EventBus.Serialization;
using SC = Tingle.EventBus.Transports.Azure.EventHubs.IotHub.IotHubJsonSerializerContext;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

internal class IotHubEventSerializer : AbstractEventSerializer
{
    private static readonly Type BaseType = typeof(IotHubEvent);

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
        if (!BaseType.IsAssignableFrom(targetType))
        {
            throw new NotSupportedException($"Only events that inherit from '{BaseType.FullName}' are supported for deserialization.");
        }

        var data = context.RawTransportData as EventData ?? throw new InvalidOperationException($"{nameof(context.RawTransportData)} cannot be null and must be an {nameof(EventData)}");

        JsonNode? telemetry = null;
        IotHubOperationalEvent? opevent = null;

        var source = Enum.Parse<IotHubEventMessageSource>(data.GetIotHubMessageSource()!, ignoreCase: true);
        if (source == IotHubEventMessageSource.Telemetry)
        {
            telemetry = await JsonNode.ParseAsync(utf8Json: stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            //var @event = new IotHubEvent { Source = source, Telemetry = telemetry, };
            //return new EventEnvelope<T> { Event = @event, };
        }
        else if (source is IotHubEventMessageSource.TwinChangeEvents
                        or IotHubEventMessageSource.DeviceLifecycleEvents
                        or IotHubEventMessageSource.DeviceConnectionStateEvents)
        {
            var hubName = data.GetRequiredPropertyValue<string>("hubName");
            var deviceId = data.GetRequiredPropertyValue<string>("deviceId");
            var moduleId = data.GetPropertyValue<string>("moduleId");
            var operationType = data.GetPropertyValue<string>("opType")!;
            var type = Enum.Parse<IotHubOperationalEventType>(operationType, ignoreCase: true);
            var operationTimestamp = data.GetPropertyValue<string>("operationTimestamp");

            var payload = await JsonSerializer.DeserializeAsync(stream, SC.Default.IotHubOperationalEventPayload, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException($"The payload of the event could not be deserialized to '{nameof(IotHubOperationalEventPayload)}'.");

            opevent = new IotHubOperationalEvent
            {
                HubName = hubName,
                DeviceId = deviceId,
                ModuleId = moduleId,
                Type = type,
                OperationTimestamp = operationTimestamp,
                Payload = payload,
            };

            //var @event = new IotHubEvent { Source = source, Event = opevent, };
        }

        var @event = (T?)Activator.CreateInstance(targetType);
        var ihe = @event as IotHubEvent ?? throw new InvalidOperationException($"The event of type '{targetType.FullName}' could not be cast to '{BaseType.FullName}'.");
        ihe.Source = source;
        ihe.Telemetry = telemetry;
        ihe.Event = opevent;
        return new EventEnvelope<T> { Event = @event, };
    }

    /// <inheritdoc/>
    protected override Task SerializeEnvelopeAsync<T>(Stream stream,
                                                      EventEnvelope<T> envelope,
                                                      CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Serialization of IotHub events is not allowed.");
    }
}
