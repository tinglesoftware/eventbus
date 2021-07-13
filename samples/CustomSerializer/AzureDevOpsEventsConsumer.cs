using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace CustomSerializer
{
    internal class AzureDevOpsEventsConsumer : IEventConsumer<AzureDevOpsCodePushed>
    {
        private readonly ILogger logger;

        public AzureDevOpsEventsConsumer(ILogger<AzureDevOpsEventsConsumer> logger)
        {
            this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public Task ConsumeAsync(EventContext<AzureDevOpsCodePushed> context, CancellationToken cancellationToken = default)
        {
            var @event = context.Event;
            var resource = @event.Resource;
            var projectUrl = resource.Repository.Project.Url;
            var project = resource.Repository.Project.Name;
            var repository = resource.Repository.Name;
            var defaultBranch = resource.Repository.DefaultBranch;

            // get the updated branchs (refs)
            var updatedReferences = resource.RefUpdates.Select(ru => ru.Name).ToList();
            logger.LogInformation("Default branch: ({DefaultBranch})", defaultBranch);
            logger.LogInformation("Updated branches (references):\r\n- {ChangedReferences}",
                                  string.Join("\r\n- ", updatedReferences));
            return Task.CompletedTask;
        }
    }
}