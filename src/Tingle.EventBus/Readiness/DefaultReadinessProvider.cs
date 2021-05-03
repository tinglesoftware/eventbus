using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Readiness
{
    internal class DefaultReadinessProvider : IReadinessProvider
    {
        private readonly IServiceScopeFactory scopeFactory;

        public DefaultReadinessProvider(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory ?? throw new System.ArgumentNullException(nameof(scopeFactory));
        }

        /// <inheritdoc/>
        public async Task<bool> IsReadyAsync(EventRegistration ereg = null,
                                             EventConsumerRegistration creg = null,
                                             CancellationToken cancellationToken = default)
        {
            /*
             * Simplest implementation is to use the health checks registered in the application.
             * 
             * If the customization is required per event or consumer, the ereg and creg arguments
             * will serve that purpose but only in a custom implementation of IReadinessProvider
             */
            using var scope = scopeFactory.CreateScope();
            var provider = scope.ServiceProvider;
            var hcs = provider.GetService<HealthCheckService>();
            if (hcs != null)
            {
                // Exclude the bus to avoid cyclic references.
                var report = await hcs.CheckHealthAsync(predicate: r => r.Tags?.Contains("eventbus") ?? false,
                                                        cancellationToken: cancellationToken);
                return report.Status == HealthStatus.Healthy;
            }

            return true;
        }
    }
}
