﻿using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace Azure.Messaging.EventHubs;

/// <summary>
/// Extension methods on <see cref="EventData"/>.
/// </summary>
public static partial class EventDataExtensions
{
    private const string IotHubPropertyNameMessageId = "message-id";
    private const string IotHubPropertyNameEnqueuedTime = "iothub-enqueuedtime";
    private const string IotHubPropertyNameDeviceId = "iothub-connection-device-id";
    private const string IotHubPropertyNameModuleId = "iothub-connection-module-id";
    private const string IotHubPropertyNameConnectionAuthGenerationId = "iothub-connection-auth-generation-id";
    private const string IotHubPropertyNameConnectionAuthMethod = "iothub-connection-auth-method";
    private const string IotHubPropertyNameMessageSource = "iothub-message-source";
    private const string IotHubPropertyNameDataSchema = "dt-dataschema";
    private const string IotHubPropertyNameSubject = "dt-subject";

    private const string IotHubMessageSourceTelemetry = "Telemetry";
    private const string IotHubMessageSourceTwinChangeEvents = "twinChangeEvents";
    private const string IotHubMessageSourceDeviceLifeCycleEvents = "deviceLifecycleEvents";

    private static bool TryGetPropertyValue(this EventData data, string key, [NotNullWhen(true)] out object? value)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace.", nameof(key));
        }

        return data.SystemProperties.TryGetValue(key, out value)
            || data.Properties.TryGetValue(key, out value);
    }

    private static T? GetPropertyValue<T>(this EventData data, string key)
    {
        return data.TryGetPropertyValue(key, out var value) && value is not null ? (T?)value : default;
    }

    /// <summary>Gets the message identifier for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubMessageId(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameMessageId);

    /// <summary>Gets the enqueued time for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static DateTime? GetIotHubEnqueuedTime(this EventData data) => data.GetPropertyValue<DateTime>(IotHubPropertyNameEnqueuedTime);

    /// <summary>Gets the device identifier for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubDeviceId(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameDeviceId);

    /// <summary>Gets whether the message is from an IoT Hub.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static bool IsIotHubMessage(this EventData data) => !string.IsNullOrEmpty(GetIotHubDeviceId(data));

    /// <summary>Gets the module identifier for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubModuleId(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameModuleId);

    /// <summary>Gets the connection authentication generation identifier for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubConnectionAuthGenerationId(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameConnectionAuthGenerationId);

    /// <summary>Gets the raw connection authentication method for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubConnectionAuthMethodRaw(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameConnectionAuthMethod);

    /// <summary>Gets the connection authentication method for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static IotHubConnectionAuthMethod? GetIotHubConnectionAuthMethod(this EventData data)
    {
        var json = data.GetIotHubConnectionAuthMethodRaw();
        if (string.IsNullOrEmpty(json)) return null;
        return System.Text.Json.JsonSerializer.Deserialize<IotHubConnectionAuthMethod>(json);
    }

    /// <summary>Gets the source for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubMessageSource(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameMessageSource);

    private static bool IsIotHubMessageIsFromSource(this EventData data, string exceptedSource) => string.Equals(exceptedSource, data.GetIotHubMessageSource());

    /// <summary>Gets whether the message is sourced from telemetry.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static bool IsIotHubTelemetry(this EventData data) => data.IsIotHubMessageIsFromSource(IotHubMessageSourceTelemetry);

    /// <summary>Gets whether the message is sourced from device/module twin changes.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static bool IsIotHubTwinChangeEvent(this EventData data) => data.IsIotHubMessageIsFromSource(IotHubMessageSourceTwinChangeEvents);

    /// <summary>Gets whether the message is sourced from lifecycle events.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static bool IsIotHubDeviceLifeCycleEvent(this EventData data) => data.IsIotHubMessageIsFromSource(IotHubMessageSourceDeviceLifeCycleEvents);

    /// <summary>Gets the data schema for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubDataSchema(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameDataSchema);

    /// <summary>Gets the subject for the IoT Hub message.</summary>
    /// <param name="data">The <see cref="EventData"/> to use.</param>
    public static string? GetIotHubSubject(this EventData data) => data.GetPropertyValue<string>(IotHubPropertyNameSubject);

}