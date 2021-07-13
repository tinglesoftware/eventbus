using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace InMemoryBackgroundProcessing
{
    internal class ProducerService : BackgroundService
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
}