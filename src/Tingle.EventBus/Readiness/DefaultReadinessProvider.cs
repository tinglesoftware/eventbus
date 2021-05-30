using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Readiness
{
    internal class DefaultReadinessProvider : IReadinessProvider
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly EventBusReadinessOptions options;
        private readonly ILogger logger;

        public DefaultReadinessProvider(IServiceScopeFactory scopeFactory,
                                        IOptions<EventBusOptions> optionsAccessor,
                                        ILogger<DefaultReadinessProvider> logger)
        {
            this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            options = optionsAccessor?.Value?.Readiness ?? throw new ArgumentNullException(nameof(optionsAccessor));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) => InternalIsReadyAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<bool> IsReadyAsync(EventRegistration ereg,
                                       EventConsumerRegistration creg,
                                       CancellationToken cancellationToken = default)
        {
            /*
             * This default implementation does not support checks per consumer/event registration.
             * Instead, it just checks as though it is the whole bus that needs to be checked.
             * 
             * To check per event/consumer, the application developer should create a custom
             * implementation of IReadinessProvider.
             */
            return InternalIsReadyAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task WaitReadyAsync(CancellationToken cancellationToken = default)
        {
            // Perform readiness check before starting bus.
            var timeout = options.Timeout;
            logger.ReadinessCheck(timeout);
            using var cts_timeout = new CancellationTokenSource(timeout);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts_timeout.Token);
            var ct = cts.Token;
            var ready = false;
            try
            {
                do
                {
                    ready = await InternalIsReadyAsync(cancellationToken: ct);
                    if (!ready)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), ct); // delay for a second
                    }
                } while (!ready);
            }
            catch (TaskCanceledException)
            {
                logger.ReadinessCheckTimedout(timeout);
                throw;
            }
        }

        private async Task<bool> InternalIsReadyAsync(CancellationToken cancellationToken)
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
                // Exclude the bus configured to do so
                Func<HealthCheckRegistration, bool> predicate = null;
                if (options.ExcludeSelf)
                {
                    predicate = r => r.Tags?.Contains("eventbus") ?? false;
                }
                var report = await hcs.CheckHealthAsync(predicate: predicate, cancellationToken: cancellationToken);
                return report.Status == HealthStatus.Healthy;
            }

            return true;
        }
    }
}
