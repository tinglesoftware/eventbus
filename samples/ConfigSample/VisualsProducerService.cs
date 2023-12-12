namespace ConfigSample;

internal class VisualsProducerService(IEventPublisher publisher, ILogger<VisualsProducerService> logger) : BackgroundService
{
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
