using Azure.Identity;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.EventBus.Transports.Azure.ServiceBus;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   var configuration = hostContext.Configuration;

                   services.AddEventBus(builder =>
                   {
                       builder.AddConsumer<VehicleTelemetryEventsConsumer>();
                       builder.AddConsumer<VisualsUploadedConsumer>();

                       // Add transports
                       builder.AddAzureServiceBusTransport();
                       builder.AddInMemoryTransport("in-memory-images");
                       builder.AddInMemoryTransport("in-memory-videos");

                       // Transport specific configuration
                       var credential = new DefaultAzureCredential();
                       builder.Services.PostConfigure<AzureServiceBusTransportOptions>(
                           name: AzureServiceBusDefaults.Name,
                           configureOptions: o => ((AzureServiceBusTransportCredentials)o.Credentials).TokenCredential = credential);
                   });

                   services.AddHostedService<VisualsProducerService>();
               })
               .Build();

await host.RunAsync();

class VisualsProducerService : BackgroundService
{
    private readonly IEventPublisher publisher;
    private readonly ILogger logger;

    public VisualsProducerService(IEventPublisher publisher, ILogger<VisualsProducerService> logger)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); // delays a little so that the logs are better visible in a better order (only ended for sample)

        logger.LogInformation("Starting production ...");

        var delay = TimeSpan.FromSeconds(20);
        var times = 10;

        var rnd = new Random(DateTimeOffset.UtcNow.Millisecond);

        for (var i = 0; i < times; i++)
        {
            var id = Convert.ToUInt32(rnd.Next()).ToString();
            var size = Convert.ToUInt32(rnd.Next());
            var image = (i % 2) == 0;
            var url = $"https://localhost:8080/{(image ? "images" : "videos")}/{id}.{(image ? "png" : "flv")}";

            _ = image
                ? await DoPublishAsync(new VideoUploaded { VideoId = id, SizeBytes = size, Url = url, }, stoppingToken)
                : await DoPublishAsync(new ImageUploaded { ImageId = id, SizeBytes = size, Url = url, }, stoppingToken);

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<ScheduledResult?> DoPublishAsync<T>(T @event, CancellationToken cancellationToken) where T : class
        => await publisher.PublishAsync(@event, cancellationToken: cancellationToken);
}

class VisualsUploadedConsumer : IEventConsumer<ImageUploaded>, IEventConsumer<VideoUploaded>
{
    private static readonly TimeSpan SimulationDuration = TimeSpan.FromSeconds(1.3f);

    private readonly ILogger logger;

    public VisualsUploadedConsumer(ILogger<VisualsUploadedConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync(EventContext<ImageUploaded> context, CancellationToken cancellationToken)
    {
        var id = context.Event.ImageId;
        var thumbnailUrl = $"https://localhost:8080/thumbnails/{id}.jpg";

        await Task.Delay(SimulationDuration, cancellationToken);
        logger.LogInformation("Generated thumbnail from image '{ImageId}' at '{ThumbnailUrl}'.", id, thumbnailUrl);
    }

    public async Task ConsumeAsync(EventContext<VideoUploaded> context, CancellationToken cancellationToken = default)
    {
        var id = context.Event.VideoId;
        var thumbnailUrl = $"https://localhost:8080/thumbnails/{id}.jpg";

        await Task.Delay(SimulationDuration, cancellationToken);
        logger.LogInformation("Generated thumbnail from video '{VideoId}' at '{ThumbnailUrl}'.", id, thumbnailUrl);
    }
}

class ImageUploaded
{
    public string? ImageId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}

class VideoUploaded
{
    public string? VideoId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}



internal class VehicleTelemetryEventsConsumer : IEventConsumer<VehicleTelemetryEvent>
{
    private readonly ILogger logger;

    public VehicleTelemetryEventsConsumer(ILogger<VehicleTelemetryEventsConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync(EventContext<VehicleTelemetryEvent> context, CancellationToken cancellationToken)
    {
        var telemetry = context.Event;

        var status = telemetry.VehicleDoorStatus;
        if (status is not VehicleDoorStatus.Open and not VehicleDoorStatus.Closed)
        {
            logger.LogWarning("Vehicle Door status '{VehicleDoorStatus}' is not yet supported", status);
            return;
        }

        var kind = telemetry.VehicleDoorKind;
        if (kind is null)
        {
            logger.LogWarning("Vehicle Door kind '{VehicleDoorKind}' cannot be null", kind);
            return;
        }

        var timestamp = telemetry.Timestamp;
        var updateEvt = new VehicleDoorOpenedEvent
        {
            VehicleId = telemetry.DeviceId,
            Kind = kind.Value,
            Closed = status is VehicleDoorStatus.Closed ? timestamp : null,
            Opened = status is VehicleDoorStatus.Open ? timestamp : null,
        };

        // the VehicleDoorOpenedEvent on a broadcast bus would notify all subscribers
        await context.PublishAsync(updateEvt, cancellationToken: cancellationToken);
    }
}

class VehicleDoorOpenedEvent
{
    public string? VehicleId { get; set; }
    public VehicleDoorKind Kind { get; set; }
    public DateTimeOffset? Opened { get; set; }
    public DateTimeOffset? Closed { get; set; }
}

class VehicleTelemetryEvent
{
    public string? DeviceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Action { get; set; }
    public VehicleDoorKind? VehicleDoorKind { get; set; }
    public VehicleDoorStatus? VehicleDoorStatus { get; set; }
    [JsonExtensionData]
    public JsonObject? Extras { get; set; }
}

enum VehicleDoorStatus { Unknown, Open, Closed, }
enum VehicleDoorKind { FrontLeft, FrontRight, RearLeft, ReadRight, Hood, Trunk, }
