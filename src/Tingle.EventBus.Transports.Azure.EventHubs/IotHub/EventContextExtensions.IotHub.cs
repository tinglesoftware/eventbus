using System.Diagnostics.CodeAnalysis;

namespace Tingle.EventBus;

/// <summary>
/// Extension methods on <see cref="EventContext"/> and <see cref="EventContext{T}"/>.
/// </summary>
public static partial class EventContextExtensions
{
    private const string IotHubPropertyNameMessageId = "message-id";
    private const string IotHubPropertyNameDeviceId = "iothub-connection-device-id";
    private const string IotHubPropertyNameModuleId = "iothub-connection-module-id";
    private const string IotHubPropertyNameMessageSource = "iothub-message-source";
    private const string IotHubPropertyNameDataSchema = "dt-dataschema";
    private const string IotHubPropertyNameSubject = "dt-subject";

    private const string IotHubMessageSourceTelemetry = "Telemetry";
    private const string IotHubMessageSourceTwinChangeEvents = "twinChangeEvents";
    private const string IotHubMessageSourceDeviceLifeCycleEvents = "deviceLifecycleEvents";

    private static bool TryGetIotHubPropertyValue(this EventContext context, string key, [NotNullWhen(true)] out object? value)
    {
        value = null;
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (context.TryGetEventData(out var eventData))
        {
            if (eventData.SystemProperties.TryGetValue(key, out value)
                || eventData.Properties.TryGetValue(key, out value)) return true;
        }

        return false;
    }

    private static string? GetIotHubStringPropertyValue(this EventContext context, string key)
    {
        if (context.TryGetIotHubPropertyValue(key, out var value))
        {
            if (value is string s) return s;
        }

        return default;
    }

    /// <summary>Gets the message identifier for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubMessageId(this EventContext context) => context.GetIotHubStringPropertyValue(IotHubPropertyNameMessageId);

    /// <summary>Gets the device identifier for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubDeviceId(this EventContext context) => context.GetIotHubStringPropertyValue(IotHubPropertyNameDeviceId);

    /// <summary>Gets whether the message is from an IoT Hub.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubMessage(this EventContext context) => !string.IsNullOrEmpty(GetIotHubDeviceId(context));

    /// <summary>Gets the module identifier for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubModuleId(this EventContext context) => context.GetIotHubStringPropertyValue(IotHubPropertyNameModuleId);

    /// <summary>Gets the source for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubMessageSource(this EventContext context) => context.GetIotHubStringPropertyValue(IotHubPropertyNameMessageSource);

    private static bool IsIotHubMessageIsFromSource(this EventContext context, string exceptedSource) => string.Equals(exceptedSource, context.GetIotHubMessageSource());

    /// <summary>Gets whether the message is sourced from telemetry.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubTelemetry(this EventContext context) => context.IsIotHubMessageIsFromSource(IotHubMessageSourceTelemetry);

    /// <summary>Gets whether the message is sourced from device/module twin changes.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubTwinChangeEvent(this EventContext context) => context.IsIotHubMessageIsFromSource(IotHubMessageSourceTwinChangeEvents);

    /// <summary>Gets whether the message is sourced from lifecycle events.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubDeviceLifeCycleEvent(this EventContext context) => context.IsIotHubMessageIsFromSource(IotHubMessageSourceDeviceLifeCycleEvents);

    /// <summary>Gets the data schema for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubDataSchema(this EventContext context) => context.GetIotHubStringPropertyValue(IotHubPropertyNameDataSchema);

    /// <summary>Gets the subject for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubSubject(this EventContext context) => context.GetIotHubStringPropertyValue(IotHubPropertyNameSubject);
}
