var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddEventBus(builder =>
                   {
                       // Transport agnostic configuration
                       builder.Configure(o =>
                       {
                           o.Naming.Scope = "dev"; // queues will be prefixed by 'dev'
                           o.Naming.UseFullTypeNames = false;
                       });
                       builder.AddConsumer<VideoUploadedConsumer>();

                       // Transport specific configuration
                       builder.AddInMemoryTransport();
                   });

                   services.AddHostedService<ProducerService>();
               })
               .Build();

await host.RunAsync();

class ProducerService : BackgroundService
{
    private readonly IEventPublisher publisher;

    public ProducerService(IEventPublisher publisher)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(25);
        var times = 5;

        var rnd = new Random(DateTimeOffset.UtcNow.Millisecond);

        for (var i = 0; i < times; i++)
        {
            var evt = new VideoUploaded
            {
                VideoId = Convert.ToUInt32(rnd.Next()).ToString(),
                SizeBytes = Convert.ToUInt32(rnd.Next()),
            };

            evt.Url = $"https://localhost:8080/uploads/raw/{evt.VideoId}.flv";

            await publisher.PublishAsync(evt, cancellationToken: stoppingToken);

            await Task.Delay(delay, stoppingToken);
        }
    }
}

class VideoUploadedConsumer : IEventConsumer<VideoUploaded>
{
    private static readonly TimeSpan SimulationDuration = TimeSpan.FromSeconds(3);

    private readonly ILogger logger;

    public VideoUploadedConsumer(ILogger<VideoUploadedConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync(EventContext<VideoUploaded> context, CancellationToken cancellationToken = default)
    {
        var evt = context.Event;
        var videoId = evt.VideoId;
        logger.LogInformation("Received event Id: {Id} for video '{VideoId}'.", context.Id, videoId);

        // Download video locally
        logger.LogInformation("Downloading video from {VideoUrl} ({VideoSize} bytes).", evt.Url, evt.SizeBytes);
        await Task.Delay(SimulationDuration, cancellationToken); // simulate using delay

        // Extract thumbnail from video
        logger.LogInformation("Extracting thumbnail from video with Id '{VideoId}'.", videoId);
        await Task.Delay(SimulationDuration, cancellationToken); // simulate using delay

        // Upload video thumbnail
        var thumbnailUrl = $"https://localhost:8080/uploads/thumbnails/{videoId}.jpg";
        logger.LogInformation("Uploading thumbnail for video with Id '{VideoId}' to '{ThumbnailUrl}'.", videoId, thumbnailUrl);
        await Task.Delay(SimulationDuration, cancellationToken); // simulate using delay

        logger.LogInformation("Processing video with Id '{VideoId}' completed.", videoId);
    }
}

class VideoUploaded
{
    public string? VideoId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}
