using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace InMemoryBackgroundProcessing
{
    public class VideoUploadedConsumer : IEventBusConsumer<VideoUploaded>
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
            await Task.Delay(SimulationDuration); // simulate using delay

            // Extract thumbnail from video
            logger.LogInformation("Extracting thumbnail from video with Id '{VideoId}'.", videoId);
            await Task.Delay(SimulationDuration); // simulate using delay

            // Upload video thumbnail
            var thumbnailUrl = $"https://localhost:8080/uploads/thumbnails/{videoId}.jpg";
            logger.LogInformation("Uploading thumbnail for video with Id '{VideoId}' to '{ThumbnailUrl}'.", videoId, thumbnailUrl);
            await Task.Delay(SimulationDuration); // simulate using delay

            logger.LogInformation("Processing video with Id '{VideoId}' completed.", videoId);
        }
    }
}
