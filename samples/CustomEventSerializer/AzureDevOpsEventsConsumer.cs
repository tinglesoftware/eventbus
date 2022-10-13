namespace CustomEventSerializer;

internal class AzureDevOpsEventsConsumer : IEventConsumer<AzureDevOpsCodePushed>
{
    private readonly ILogger logger;

    public AzureDevOpsEventsConsumer(ILogger<AzureDevOpsEventsConsumer> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ConsumeAsync(EventContext<AzureDevOpsCodePushed> context, CancellationToken cancellationToken = default)
    {
        var @event = context.Event;
        var resource = @event.Resource;
        var repository = resource?.Repository;
        var defaultBranch = repository?.DefaultBranch;

        // get the updated branches (refs)
        var updatedReferences = resource?.RefUpdates?.Select(ru => ru.Name).ToList() ?? new List<string?>();
        logger.LogInformation("Default branch: ({DefaultBranch})", defaultBranch);
        logger.LogInformation("Updated branches (references):\r\n- {ChangedReferences}",
                              string.Join("\r\n- ", updatedReferences));
        return Task.CompletedTask;
    }
}
