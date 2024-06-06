using System.Text.Json.Serialization;
using Tingle.EventBus.Serialization;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddSlimEventBus(CustomSrializerContext.Default, builder =>
                   {
                       builder.AddConsumer<VideoUploaded, VideoUploadedConsumer>();
                       builder.AddDeadLetteredConsumer<VideoUploaded, VideoUploadedConsumer>();

                       builder.AddInMemoryTransport();
                   });

                   services.AddHostedService<ProducerService>();
               })
               .Build();

await host.RunAsync();

class ProducerService(IEventPublisher publisher) : BackgroundService
{
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

class VideoUploadedConsumer(ILogger<VideoUploadedConsumer> logger) : IEventConsumer<VideoUploaded>, IDeadLetteredEventConsumer<VideoUploaded>
{
    private static readonly TimeSpan SimulationDuration = TimeSpan.FromSeconds(3);

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

    public Task ConsumeAsync(DeadLetteredEventContext<VideoUploaded> context, CancellationToken cancellationToken)
    {
        logger.LogWarning("Event with Id '{Id}' for video '{VideoId}' was dead-lettered.", context.Id, context.Event.VideoId);
        return Task.CompletedTask;
    }
}

class VideoUploaded
{
    public string? VideoId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}

[JsonSerializable(typeof(EventEnvelope<VideoUploaded>))]
partial class CustomSrializerContext : JsonSerializerContext { }
