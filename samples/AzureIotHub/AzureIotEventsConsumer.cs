namespace AzureIotHub;

internal class AzureIotEventsConsumer : IEventConsumer<MyIotHubEvent>
{
    private readonly ILogger logger;

    public AzureIotEventsConsumer(ILogger<AzureIotEventsConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ConsumeAsync(EventContext<MyIotHubEvent> context, CancellationToken cancellationToken)
    {
        var deviceId = context.GetIotHubDeviceId();
        var source = context.GetIotHubMessageSource();
        var enqueued = context.GetIotHubEnqueuedTime();

        logger.LogInformation("Received {Source} from {DeviceId}\r\nEnqueued: {EnqueuedTime}\r\nTimestamped: {Timestamp}.",
                              source,
                              deviceId,
                              enqueued,
                              context.Event.Timestamp);

        return Task.CompletedTask;
    }
}
