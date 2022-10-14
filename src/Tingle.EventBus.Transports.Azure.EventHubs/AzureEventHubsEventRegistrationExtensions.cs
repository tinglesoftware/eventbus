using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

namespace Tingle.EventBus.Configuration;

/// <summary>
/// Extension methods on <see cref="EventRegistration"/>.
/// </summary>
public static class AzureEventHubsEventRegistrationExtensions
{
    internal const string MetadataKeyIotHubEventHubName = "azure.iothub.eventhub-name";

    /// <summary>
    /// Configure the event hub name to be used when connected to the inbuilt Event Hubs endpoint for an Azure IoT Hub instance.
    /// </summary>
    /// <param name="registration">The <see cref="EventRegistration"/> to configure.</param>
    /// <param name="name">The Event Hub-compatible name e.g. <c>iothub-ehub-test-dev-1973449-3febe524cb</c></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static EventRegistration ConfigureAsIotHubEvent(this EventRegistration registration, [NotNull] string name)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace.", nameof(name));
        }

        registration.Metadata[MetadataKeyIotHubEventHubName] = name;

        return registration;
    }

    /// <summary>
    /// Use the serializer that supports <see cref="IotHubEvent{TDeviceTelemetry, TDeviceTwinChange, TDeviceLifecycle}"/>.
    /// </summary>
    /// <param name="registration">The <see cref="EventRegistration"/> to configure.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static EventRegistration UseIotHubEventSerializer(this EventRegistration registration)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));

        registration.EventSerializerType = typeof(IotHubEventSerializer);

        return registration;
    }

    internal static string GetIotHubEventHubName(this EventRegistration registration)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));

        return (string)registration.Metadata[MetadataKeyIotHubEventHubName];
    }

    internal static bool IsConfiguredAsIotHub(this EventRegistration registration)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));

        return registration.Metadata.ContainsKey(MetadataKeyIotHubEventHubName);
    }
}
