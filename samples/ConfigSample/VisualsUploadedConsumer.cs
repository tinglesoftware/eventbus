namespace ConfigSample;

internal class VisualsUploadedConsumer(ILogger<VisualsUploadedConsumer> logger) : IEventConsumer<ImageUploaded>, IEventConsumer<VideoUploaded>
{
    private static readonly TimeSpan SimulationDuration = TimeSpan.FromSeconds(1.3f);

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
