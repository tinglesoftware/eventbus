using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>Represents an event from Azure IoT Hub.</summary>
public record IotHubEvent
{
    /// <summary>The source of the event.</summary>
    public IotHubEventMessageSource Source { get; internal set; }

    /// <summary>Whether the event is a telemetry event.</summary>
    [MemberNotNullWhen(true, nameof(Telemetry))]
    [MemberNotNullWhen(false, nameof(Event))]
    public bool IsTelemetry => Source is IotHubEventMessageSource.Telemetry;

    /// <summary>Whether the event is a twin change event.</summary>
    [MemberNotNullWhen(false, nameof(Telemetry))]
    [MemberNotNullWhen(true, nameof(Event))]
    public bool IsTwinEvent => Source is IotHubEventMessageSource.TwinChangeEvents;

    /// <summary>Whether the event is a lifecycle event.</summary>
    [MemberNotNullWhen(false, nameof(Telemetry))]
    [MemberNotNullWhen(true, nameof(Event))]
    public bool IsLifecycleEvent => Source is IotHubEventMessageSource.DeviceLifecycleEvents;

    /// <summary>Whether the event is a connection state event.</summary>
    [MemberNotNullWhen(false, nameof(Telemetry))]
    [MemberNotNullWhen(true, nameof(Event))]
    public bool IsConnectionStateEvent => Source is IotHubEventMessageSource.DeviceConnectionStateEvents;

    /// <summary>
    /// The telemetry data.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.Telemetry"/>.
    /// </summary>
    public JsonNode? Telemetry { get; internal set; }

    /// <summary>
    /// The connection state event.
    /// Only populate when <see cref="Source"/> is set to
    /// <see cref="IotHubEventMessageSource.DeviceConnectionStateEvents"/>.
    /// </summary>
    public IotHubOperationalEvent? Event { get; internal set; }

    #region Telemetry to other types

    /// <summary>Create a <typeparamref name="TValue"/> from the template's backing object.</summary>
    /// <typeparam name="TValue">The type to deserialize the template into.</typeparam>
    /// <param name="options">Options to control the conversion behavior.</param>
    /// <returns>A <typeparamref name="TValue"/> representation of the template.</returns>
    /// <exception cref="InvalidOperationException">
    /// The model does not container a backing object.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// There is no compatible <see cref="System.Text.Json.Serialization.JsonConverter"/>
    /// for <typeparamref name="TValue"/> or its serializable members.
    /// </exception>
    [RequiresUnreferencedCode(MessageStrings.SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(MessageStrings.SerializationRequiresDynamicCodeMessage)]
    public TValue GetTelemetry<TValue>(JsonSerializerOptions? options = null)
    {
        if (Telemetry is null) throw new InvalidOperationException("Telemetry is null. This method can only be called when the source is telemetry");

        return JsonSerializer.Deserialize<TValue>(Telemetry, options: options) ?? throw new InvalidOperationException($"The telemetry could not be deserialized to '{typeof(TValue)}'.");
    }

    /// <summary>Create a <typeparamref name="TValue"/> from the template's backing object.</summary>
    /// <typeparam name="TValue">The type to deserialize the template into.</typeparam>
    /// <param name="jsonTypeInfo">Metadata about the type to convert.</param>
    /// <exception cref="InvalidOperationException">
    /// The model does not container a backing object.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="jsonTypeInfo"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="JsonException">
    /// <typeparamref name="TValue" /> is not compatible with the JSON.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// There is no compatible <see cref="System.Text.Json.Serialization.JsonConverter"/>
    /// for <typeparamref name="TValue"/> or its serializable members.
    /// </exception>
    public TValue GetTelemetry<TValue>(JsonTypeInfo<TValue> jsonTypeInfo)
    {
        if (Telemetry is null) throw new InvalidOperationException("Telemetry is null. This method can only be called when the source is telemetry");

        return JsonSerializer.Deserialize(Telemetry, jsonTypeInfo: jsonTypeInfo) ?? throw new InvalidOperationException($"The telemetry could not be deserialized to '{typeof(TValue)}'.");
    }

    /// <summary>
    /// Converts the template's backing object into a <paramref name="returnType"/>.
    /// </summary>
    /// <param name="returnType">The type of the object to convert to and return.</param>
    /// <param name="context">A metadata provider for serializable types.</param>
    /// <returns>A <paramref name="returnType"/> representation of the JSON value.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="returnType"/> is <see langword="null"/>.
    ///
    /// -or-
    ///
    /// <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="JsonException">
    /// The JSON is invalid.
    ///
    /// -or-
    ///
    /// <paramref name="returnType" /> is not compatible with the JSON.
    ///
    /// -or-
    ///
    /// There is remaining data in the string beyond a single JSON value.</exception>
    /// <exception cref="NotSupportedException">
    /// There is no compatible <see cref="System.Text.Json.Serialization.JsonConverter"/>
    /// for <paramref name="returnType"/> or its serializable members.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The <see cref="JsonSerializerContext.GetTypeInfo(Type)"/> method of the provided
    /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
    /// </exception>
    public object GetTelemetry(Type returnType, JsonSerializerContext context)
    {
        if (Telemetry is null) throw new InvalidOperationException("Telemetry is null. This method can only be called when the source is telemetry");

        return JsonSerializer.Deserialize(Telemetry, returnType: returnType, context: context) ?? throw new InvalidOperationException($"The telemetry could not be deserialized to '{returnType}'.");
    }

    #endregion
}

/// <summary>The kind of source for an Azure IoT Hub Event.</summary>
public enum IotHubEventMessageSource
{
    ///
    Telemetry,

    ///
    TwinChangeEvents,

    ///
    DeviceLifecycleEvents,

    ///
    DeviceConnectionStateEvents,
}

/// <summary>Represents an operational event from Azure IoT Hub.</summary>
public sealed record IotHubOperationalEvent
{
    /// <summary>The name of the hub where the event happened.</summary>
    public required string HubName { get; init; }

    /// <summary>Unique identifier of the device.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Unique identifier of the module within the device.</summary>
    public string? ModuleId { get; init; }

    /// <summary>Type of operational event.</summary>
    public required IotHubOperationalEventType Type { get; init; }

    /// <summary>Time when the operation was done.</summary>
    public string? OperationTimestamp { get; init; }

    /// <summary>The actual event.</summary>
    public required IotHubOperationalEventPayload Payload { get; init; }
}

/// <summary>Abstractions for an Azure IoT Hub Event.</summary>
public record IotHubOperationalEventPayload
{
    /// <summary>Unique identifier of the device.</summary>
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    /// <summary>Unique identifier of the module within the device.</summary>
    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; set; }

    ///
    [JsonPropertyName("etag")]
    public string? Etag { get; set; }

    /// <summary>The version.</summary>
    [JsonPropertyName("version")]
    public long Version { get; set; }

    /// <summary>The twin properties.</summary>
    [JsonPropertyName("properties")]
    public IotHubTwinPropertiesCollection? Properties { get; set; }

    ///
    [JsonPropertyName("sequenceNumber")]
    public string? SequenceNumber { get; set; }

    ///
    [JsonExtensionData]
    public Dictionary<string, object>? Extras { get; set; }
}

/// <summary>Properties of the device twin.</summary>
public record IotHubTwinPropertiesCollection
{
    /// 
    [JsonPropertyName("desired")]
    public IotHubTwinProperties? Desired { get; set; }

    /// 
    [JsonPropertyName("reported")]
    public IotHubTwinProperties? Reported { get; set; }
}

/// <summary>Properties of the device twin.</summary>
public record IotHubTwinProperties
{
    /// <summary>The version of the twin.</summary>
    [JsonPropertyName("$version")]
    public long Version { get; set; }

    ///
    [JsonExtensionData]
    public Dictionary<string, object>? Extras { get; set; }
}

/// <summary>The type of an operational event on Azure IoT Hub.</summary>
public enum IotHubOperationalEventType
{
    ///
    UpdateTwin,

    ///
    ReplaceTwin,

    ///
    CreateDeviceIdentity,

    ///
    DeleteDeviceIdentity,

    ///
    DeviceDisconnected,

    ///
    DeviceConnected,
}
