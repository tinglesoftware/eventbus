using Azure.Messaging.EventHubs;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace Tingle.EventBus;

/// <summary>
/// Extension methods on <see cref="EventContext"/> and <see cref="EventContext{T}"/>.
/// </summary>
public static partial class EventContextExtensions
{
    /// <summary>Gets the message identifier for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubMessageId(this EventContext context) => context.GetEventData().GetIotHubMessageId();

    /// <summary>Gets the enqueued time for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static DateTime? GetIotHubEnqueuedTime(this EventContext context) => context.GetEventData().GetIotHubEnqueuedTime();

    /// <summary>Gets the device identifier for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubDeviceId(this EventContext context) => context.GetEventData().GetIotHubDeviceId();

    /// <summary>Gets whether the message is from an IoT Hub.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubMessage(this EventContext context) => !string.IsNullOrEmpty(GetIotHubDeviceId(context));

    /// <summary>Gets the module identifier for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubModuleId(this EventContext context) => context.GetEventData().GetIotHubModuleId();

    /// <summary>Gets the connection authentication generation identifier for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubConnectionAuthGenerationId(this EventContext context) => context.GetEventData().GetIotHubConnectionAuthGenerationId();

    /// <summary>Gets the raw connection authentication method for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubConnectionAuthMethodRaw(this EventContext context) => context.GetEventData().GetIotHubConnectionAuthMethodRaw();

    /// <summary>Gets the connection authentication method for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static IotHubConnectionAuthMethod? GetIotHubConnectionAuthMethod(this EventContext context) => context.GetEventData().GetIotHubConnectionAuthMethod();

    /// <summary>Gets the source for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubMessageSource(this EventContext context) => context.GetEventData().GetIotHubMessageSource();

    /// <summary>Gets whether the message is sourced from telemetry.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubTelemetry(this EventContext context) => context.GetEventData().IsIotHubTelemetry();

    /// <summary>Gets whether the message is sourced from device/module twin changes.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubTwinChangeEvent(this EventContext context) => context.GetEventData().IsIotHubTwinChangeEvent();

    /// <summary>Gets whether the message is sourced from lifecycle events.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static bool IsIotHubDeviceLifecycleEvent(this EventContext context) => context.GetEventData().IsIotHubDeviceLifecycleEvent();

    /// <summary>Gets the data schema for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubDataSchema(this EventContext context) => context.GetEventData().GetIotHubDataSchema();

    /// <summary>Gets the subject for the IoT Hub message.</summary>
    /// <param name="context">The <see cref="EventContext"/> to use.</param>
    public static string? GetIotHubSubject(this EventContext context) => context.GetEventData().GetIotHubSubject();
}
