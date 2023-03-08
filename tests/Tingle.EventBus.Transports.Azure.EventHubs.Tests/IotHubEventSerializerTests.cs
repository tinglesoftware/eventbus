using Azure.Messaging.EventHubs;
using AzureIotHub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Mime;
using System.Text.Json;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Serialization;
using Tingle.EventBus.Transports.Azure.EventHubs.IotHub;
using Xunit.Abstractions;

namespace Tingle.EventBus.Transports.Azure.EventHubs.Tests;

public class IotHubEventSerializerTests
{
    private const string DeviceId = "1234567890";
    private const string HubName = "iothub-route-test-weu-ih";

    private readonly ITestOutputHelper outputHelper;

    public IotHubEventSerializerTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    private static (EventData, DeserializationContext) CreateData(EventRegistration ereg, BinaryData body, string source, IDictionary<string, object>? properties = null)
    {
        var ed = new EventData(body);
        ed.Properties["iothub-message-source"] = source;
        ed.Properties["iothub-connection-device-id"] = DeviceId;
        if (properties is not null)
            foreach (var (key, value) in properties)
                ed.Properties[key] = value;

        var ctx = new DeserializationContext(ed.EventBody, ereg)
        {
            RawTransportData = ed,
            ContentType = new ContentType("application/json"),
        };
        return (ed, ctx);
    }

    [Fact]
    public async Task DeserializeAsync_Works_For_Telemetry()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = new EventRegistration(typeof(MyIotHubEvent));
            var stream = TestSamples.GetIotHubTelemetry();
            var (ed, ctx) = CreateData(ereg, await BinaryData.FromStreamAsync(stream), "Telemetry");
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.TwinEvent);
            Assert.Null(envelope.Event.LifecycleEvent);
            Assert.Null(envelope.Event.ConnectionStateEvent);
            var telemetry = Assert.IsType<MyIotHubTelemetry>(envelope.Event.Telemetry);
            Assert.Equal(DateTimeOffset.Parse("2022-12-22T12:10:51Z"), telemetry.Timestamp);
            Assert.Equal(JsonSerializer.SerializeToNode(new { door = "frontLeft" })!.ToJsonString(), JsonSerializer.Serialize(telemetry.Extras));
        });
    }

    [Fact]
    public async Task DeserializeAsync_Works_For_TwinChangeEvents()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = new EventRegistration(typeof(MyIotHubEvent));
            var stream = TestSamples.GetIotHubTwinChangeEvents();
            var (ed, ctx) = CreateData(ereg, await BinaryData.FromStreamAsync(stream), "twinChangeEvents", new Dictionary<string, object>
            {
                ["hubName"] = HubName,
                ["deviceId"] = DeviceId,
                ["opType"] = "updateTwin",
                ["operationTimestamp"] = "2022-01-16T16:36:53.8146535Z",
                ["iothub-message-schema"] = "twinChangeNotification",
            });
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.Telemetry);
            Assert.Null(envelope.Event.LifecycleEvent);
            Assert.Null(envelope.Event.ConnectionStateEvent);
            var twinEvent = Assert.IsType<IotHubOperationalEvent<IotHubDeviceTwinChangeEvent>>(envelope.Event.TwinEvent);
            Assert.Equal(HubName, twinEvent.HubName);
            Assert.Equal(DeviceId, twinEvent.DeviceId);
            Assert.Null(twinEvent.ModuleId);
            Assert.Equal(IotHubOperationalEventType.UpdateTwin, twinEvent.Type);
            Assert.Equal("2022-01-16T16:36:53.8146535Z", twinEvent.OperationTimestamp);
            Assert.Null(twinEvent.Event.DeviceId);
            Assert.Null(twinEvent.Event.ModuleId);
            Assert.Null(twinEvent.Event.Etag);
            Assert.Equal(3, twinEvent.Event.Version);
            Assert.Equal(2, twinEvent.Event.Properties?.Desired?.Version);
        });
    }

    [Fact]
    public async Task DeserializeAsync_Works_For_DeviceLifecycleEvents()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = new EventRegistration(typeof(MyIotHubEvent));
            var stream = TestSamples.GetIotHubDeviceLifecycleEvents();
            var (ed, ctx) = CreateData(ereg, await BinaryData.FromStreamAsync(stream), "deviceLifecycleEvents", new Dictionary<string, object>
            {
                ["hubName"] = HubName,
                ["deviceId"] = DeviceId,
                ["opType"] = "createDeviceIdentity",
                ["operationTimestamp"] = "2022-01-16T16:36:53.8146535Z",
                ["iothub-message-schema"] = "deviceLifecycleNotification",
            });
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.Telemetry);
            Assert.Null(envelope.Event.TwinEvent);
            Assert.Null(envelope.Event.ConnectionStateEvent);
            var lifecycleEvent = Assert.IsType<IotHubOperationalEvent<IotHubDeviceLifecycleEvent>>(envelope.Event.LifecycleEvent);
            Assert.Equal(HubName, lifecycleEvent.HubName);
            Assert.Equal(DeviceId, lifecycleEvent.DeviceId);
            Assert.Null(lifecycleEvent.ModuleId);
            Assert.Equal(IotHubOperationalEventType.CreateDeviceIdentity, lifecycleEvent.Type);
            Assert.Equal("2022-01-16T16:36:53.8146535Z", lifecycleEvent.OperationTimestamp);
            Assert.Equal(DeviceId, lifecycleEvent.Event.DeviceId);
            Assert.Null(lifecycleEvent.Event.ModuleId);
            Assert.Equal("AAAAAAAAAAE=", lifecycleEvent.Event.Etag);
            Assert.Equal(2, lifecycleEvent.Event.Version);
        });
    }

    [Fact]
    public async Task DeserializeAsync_Works_For_DeviceConnectionStateEvents()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = new EventRegistration(typeof(MyIotHubEvent));
            var stream = TestSamples.GetIotHubDeviceConnectionStateEvents();
            var (ed, ctx) = CreateData(ereg, await BinaryData.FromStreamAsync(stream), "deviceConnectionStateEvents", new Dictionary<string, object>
            {
                ["hubName"] = HubName,
                ["deviceId"] = DeviceId,
                ["opType"] = "deviceConnected",
                ["operationTimestamp"] = "2022-01-16T16:36:53.8146535Z",
                ["iothub-message-schema"] = "deviceConnectionStateNotification",
            });
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.Telemetry);
            Assert.Null(envelope.Event.TwinEvent);
            Assert.Null(envelope.Event.LifecycleEvent);
            var connectionStateEvent = Assert.IsType<IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>>(envelope.Event.ConnectionStateEvent);
            Assert.Equal(HubName, connectionStateEvent.HubName);
            Assert.Equal(DeviceId, connectionStateEvent.DeviceId);
            Assert.Null(connectionStateEvent.ModuleId);
            Assert.Equal(IotHubOperationalEventType.DeviceConnected, connectionStateEvent.Type);
            Assert.Equal("2022-01-16T16:36:53.8146535Z", connectionStateEvent.OperationTimestamp);
            Assert.Equal("000000000000000001D7F744182052190000000C000000000000000000000001", connectionStateEvent.Event.SequenceNumber);
        });
    }

    [Fact]
    public async Task DeserializeAsync_Throws_NotSupportedException_For_UnsupportedTypes()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = new EventRegistration(typeof(DummyEvent1));
            var ctx = new DeserializationContext(BinaryData.FromString(""), ereg);
            var ex = await Assert.ThrowsAsync<NotSupportedException>(() => serializer.DeserializeAsync<DummyEvent1>(ctx));
            Assert.Equal("Only events that inherit from 'Tingle.EventBus.Transports.Azure.EventHubs.IotHub.IotHubEvent`3' are supported for deserialization.", ex.Message);
        });
    }

    [Fact]
    public async Task DeserializeAsync_Throws_NotSupportedException_For_AbstractTypes()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = new EventRegistration(typeof(DummyEvent2));
            var ctx = new DeserializationContext(BinaryData.FromString(""), ereg);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => serializer.DeserializeAsync<DummyEvent2>(ctx));
            Assert.Equal($"Abstract type '{typeof(DummyTelemetry1).FullName}' on '{typeof(DummyEvent2).FullName}' is not supported for IotHub events.", ex.Message);
        });
    }

    [Fact]
    public async Task SerializeAsync_Throws_NotSupportedException()
    {
        await TestSerializerAsync(async (provider, publisher, serializer) =>
        {
            var ereg = new EventRegistration(typeof(MyIotHubEvent));
            var context = new EventContext<MyIotHubEvent>(publisher, new MyIotHubEvent(IotHubEventMessageSource.Telemetry, null, null, null, null));
            var ctx = new SerializationContext<MyIotHubEvent>(context, ereg);
            var ex = await Assert.ThrowsAsync<NotSupportedException>(() => serializer.SerializeAsync(ctx));
            Assert.Equal("Serialization of IotHub events is not allowed.", ex.Message);
        });
    }

    private async Task TestSerializerAsync(Func<IServiceProvider, IEventPublisher, IotHubEventSerializer, Task> executeAndVerify)
    {
        var host = Host.CreateDefaultBuilder()
                       .ConfigureAppConfiguration(builder =>
                       {
                           builder.AddInMemoryCollection(new Dictionary<string, string?>
                           {
                               [$"EventBus:Transports:azure-event-hubs:ConnectionString"] = "dummy-cs",
                           });
                       })
                       .ConfigureServices((context, services) =>
                       {
                           services.AddEventBus(builder =>
                           {
                               builder.AddAzureEventHubsTransport();
                               builder.Services.AddSingleton<IotHubEventSerializer>();
                           });
                       })
                       .ConfigureLogging((context, builder) => builder.AddXUnit(outputHelper))
                       .Build();

        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var serializer = provider.GetRequiredService<IotHubEventSerializer>();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        await executeAndVerify(provider, publisher, serializer);
    }

    class DummyEvent1 { }

    abstract class DummyTelemetry1 { }
    record DummyEvent2 : IotHubEvent<DummyTelemetry1>
    {
        public DummyEvent2(IotHubEventMessageSource source,
                           DummyTelemetry1? telemetry,
                           IotHubOperationalEvent<IotHubDeviceTwinChangeEvent>? twinEvent,
                           IotHubOperationalEvent<IotHubDeviceLifecycleEvent>? lifecycleEvent,
                           IotHubOperationalEvent<IotHubDeviceConnectionStateEvent>? connectionStateEvent)
            : base(source, telemetry, twinEvent, lifecycleEvent, connectionStateEvent) { }
    }
}
