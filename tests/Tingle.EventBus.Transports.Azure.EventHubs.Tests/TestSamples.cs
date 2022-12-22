namespace Tingle.EventBus.Transports.Azure.EventHubs.Tests;

internal class TestSamples
{
    public static Stream GetResourceAsStream(string fileName)
        => typeof(TestSamples).Assembly.GetManifestResourceStream(string.Join(".", typeof(TestSamples).Namespace, "Samples", fileName))!;

    public static Stream GetIotHubTelemetry() => GetResourceAsStream("iot-hub-Telemetry.json");
    public static Stream GetIotHubTwinChangeEvents() => GetResourceAsStream("iot-hub-twinChangeEvents.json");
    public static Stream GetIotHubDeviceLifecycleEvents() => GetResourceAsStream("iot-hub-deviceLifecycleEvents.json");
    public static Stream GetIotHubDeviceConnectionStateEvents() => GetResourceAsStream("iot-hub-deviceConnectionStateEvents.json");
}
