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

namespace Tingle.EventBus.Transports.Azure.EventHubs.Tests;

public class IotHubEventSerializerTests(ITestOutputHelper outputHelper)
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    private const string DeviceId = "1234567890";
    private const string HubName = "iothub-route-test-weu-ih";

    private static (EventData, DeserializationContext) CreateData(EventRegistration ereg, BinaryData body, string source, IDictionary<string, object>? properties = null)
    {
        var ed = new EventData(body);
        ed.Properties["iothub-message-source"] = source;
        ed.Properties["iothub-connection-device-id"] = DeviceId;
        if (properties is not null)
            foreach (var (key, value) in properties)
                ed.Properties[key] = value;

        var ctx = new DeserializationContext(ed.EventBody, ereg, false)
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
            var ereg = EventRegistration.Create<MyIotHubEvent>();
            var stream = TestSamples.GetIotHubTelemetry();

            var (ed, ctx) = CreateData(ereg, await BinaryData.FromStreamAsync(stream, TestContext.Current.CancellationToken), "Telemetry");
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx, TestContext.Current.CancellationToken);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.Event);
            var telemetry = envelope.Event.GetTelemetry<MyIotHubTelemetry>(serializerOptions);
            Assert.Equal(DateTimeOffset.Parse("2022-12-22T12:10:51Z"), telemetry.Timestamp);
            Assert.Equal(JsonSerializer.SerializeToNode(new { door = "frontLeft" })!.ToJsonString(), JsonSerializer.Serialize(telemetry.Extras));
        });
    }

    [Fact]
    public async Task DeserializeAsync_Works_For_TwinChangeEvents()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = EventRegistration.Create<MyIotHubEvent>();
            var stream = TestSamples.GetIotHubTwinChangeEvents();

            var (ed, ctx) = CreateData(ereg,
                                       await BinaryData.FromStreamAsync(stream, TestContext.Current.CancellationToken),
                                       "twinChangeEvents",
                                       new Dictionary<string, object>
                                       {
                                           ["hubName"] = HubName,
                                           ["deviceId"] = DeviceId,
                                           ["opType"] = "updateTwin",
                                           ["operationTimestamp"] = "2022-01-16T16:36:53.8146535Z",
                                           ["iothub-message-schema"] = "twinChangeNotification",
                                       });
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx, TestContext.Current.CancellationToken);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.Telemetry);
            var opevent = envelope.Event.Event;
            Assert.NotNull(opevent);
            Assert.Equal(HubName, opevent.HubName);
            Assert.Equal(DeviceId, opevent.DeviceId);
            Assert.Null(opevent.ModuleId);
            Assert.Equal(IotHubOperationalEventType.UpdateTwin, opevent.Type);
            Assert.Equal("2022-01-16T16:36:53.8146535Z", opevent.OperationTimestamp);
            Assert.Null(opevent.Payload.DeviceId);
            Assert.Null(opevent.Payload.ModuleId);
            Assert.Null(opevent.Payload.Etag);
            Assert.Equal(3, opevent.Payload.Version);
            Assert.Equal(2, opevent.Payload.Properties?.Desired?.Version);
        });
    }

    [Fact]
    public async Task DeserializeAsync_Works_For_DeviceLifecycleEvents()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = EventRegistration.Create<MyIotHubEvent>();
            var stream = TestSamples.GetIotHubDeviceLifecycleEvents();

            var (ed, ctx) = CreateData(ereg,
                                       await BinaryData.FromStreamAsync(stream, TestContext.Current.CancellationToken),
                                       "deviceLifecycleEvents",
                                       new Dictionary<string, object>
                                       {
                                           ["hubName"] = HubName,
                                           ["deviceId"] = DeviceId,
                                           ["opType"] = "createDeviceIdentity",
                                           ["operationTimestamp"] = "2022-01-16T16:36:53.8146535Z",
                                           ["iothub-message-schema"] = "deviceLifecycleNotification",
                                       });
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx, TestContext.Current.CancellationToken);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.Telemetry);
            var opevent = envelope.Event.Event;
            Assert.NotNull(opevent);
            Assert.Equal(HubName, opevent.HubName);
            Assert.Equal(DeviceId, opevent.DeviceId);
            Assert.Null(opevent.ModuleId);
            Assert.Equal(IotHubOperationalEventType.CreateDeviceIdentity, opevent.Type);
            Assert.Equal("2022-01-16T16:36:53.8146535Z", opevent.OperationTimestamp);
            Assert.Equal(DeviceId, opevent.Payload.DeviceId);
            Assert.Null(opevent.Payload.ModuleId);
            Assert.Equal("AAAAAAAAAAE=", opevent.Payload.Etag);
            Assert.Equal(2, opevent.Payload.Version);
        });
    }

    [Fact]
    public async Task DeserializeAsync_Works_For_DeviceConnectionStateEvents()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = EventRegistration.Create<MyIotHubEvent>();
            var stream = TestSamples.GetIotHubDeviceConnectionStateEvents();
            var (ed, ctx) = CreateData(ereg,
                                       await BinaryData.FromStreamAsync(stream, TestContext.Current.CancellationToken),
                                       "deviceConnectionStateEvents",
                                       new Dictionary<string, object>
                                       {
                                           ["hubName"] = HubName,
                                           ["deviceId"] = DeviceId,
                                           ["opType"] = "deviceConnected",
                                           ["operationTimestamp"] = "2022-01-16T16:36:53.8146535Z",
                                           ["iothub-message-schema"] = "deviceConnectionStateNotification",
                                       });
            var envelope = await serializer.DeserializeAsync<MyIotHubEvent>(ctx, TestContext.Current.CancellationToken);
            Assert.NotNull(envelope);
            Assert.NotNull(envelope.Event);
            Assert.Null(envelope.Event.Telemetry);
            var opevent = envelope.Event.Event;
            Assert.NotNull(opevent);
            Assert.Equal(HubName, opevent.HubName);
            Assert.Equal(DeviceId, opevent.DeviceId);
            Assert.Null(opevent.ModuleId);
            Assert.Equal(IotHubOperationalEventType.DeviceConnected, opevent.Type);
            Assert.Equal("2022-01-16T16:36:53.8146535Z", opevent.OperationTimestamp);
            Assert.Equal("000000000000000001D7F744182052190000000C000000000000000000000001", opevent.Payload.SequenceNumber);
        });
    }

    [Fact]
    public async Task DeserializeAsync_Throws_NotSupportedException_For_UnsupportedTypes()
    {
        await TestSerializerAsync(async (provider, _, serializer) =>
        {
            var ereg = EventRegistration.Create<DummyEvent1>();
            var ctx = new DeserializationContext(BinaryData.FromString(""), ereg, false);
            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => serializer.DeserializeAsync<DummyEvent1>(ctx, TestContext.Current.CancellationToken));
            Assert.Equal("Only events that inherit from 'Tingle.EventBus.Transports.Azure.EventHubs.IotHub.IotHubEvent' are supported for deserialization.", ex.Message);
        });
    }

    [Fact]
    public async Task SerializeAsync_Throws_NotSupportedException()
    {
        await TestSerializerAsync(async (provider, publisher, serializer) =>
        {
            var ereg = EventRegistration.Create<MyIotHubEvent>();
            var context = new EventContext<MyIotHubEvent>(publisher, new());
            var ctx = new SerializationContext<MyIotHubEvent>(context, ereg);
            var ex = await Assert.ThrowsAsync<NotSupportedException>(() => serializer.SerializeAsync(ctx, TestContext.Current.CancellationToken));
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
    record DummyEvent2 : IotHubEvent { }
}
