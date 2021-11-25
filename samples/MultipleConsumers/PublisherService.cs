namespace MultipleConsumers;

public class PublisherService : BackgroundService
{
    private readonly IEventPublisher publisher;

    public PublisherService(IEventPublisher publisher)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(30);
        var times = 5;

        var rnd = new Random(DateTimeOffset.UtcNow.Millisecond);

        for (var i = 0; i < times; i++)
        {
            var evt = new DoorOpened
            {
                Kind = (OpenDoorKind)rnd.Next(0, 5),
                Opened = DateTimeOffset.UtcNow.AddMinutes(rnd.Next(-10, 10)),
                VehicleId = "123456",
            };

            await publisher.PublishAsync(evt, cancellationToken: stoppingToken);

            await Task.Delay(delay, stoppingToken);
        }
    }
}
