using Tingle.EventBus.Configuration;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddEventBus(builder =>
                   {
                       // Transport agnostic configuration
                       builder.Configure(o => o.Naming.UseFullTypeNames = false);

                       builder.AddConsumer<VisualsUploadedConsumer>();

                       // Add transports
                       builder.AddInMemoryTransport("in-memory-images");
                       builder.AddInMemoryTransport("in-memory-videos");
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

        var delay = TimeSpan.FromSeconds(10);
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

[EventTransportName("in-memory-images")] // can also be configured in the builder
class ImageUploaded
{
    public string? ImageId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}

[EventTransportName("in-memory-videos")] // can also be configured in the builder
class VideoUploaded
{
    public string? VideoId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}
