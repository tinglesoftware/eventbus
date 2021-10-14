using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Readiness
{
    internal class DefaultReadinessProvider : IReadinessProvider, IHealthCheckPublisher
    {
        private readonly EventBusReadinessOptions options;
        private readonly ILogger logger;

        private HealthReport? healthReport;

        public DefaultReadinessProvider(IOptions<EventBusOptions> optionsAccessor,
                                        ILogger<DefaultReadinessProvider> logger)
        {
            options = optionsAccessor?.Value?.Readiness ?? throw new ArgumentNullException(nameof(optionsAccessor));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(InternalIsReady(allowed: null /* no filters */));

        /// <inheritdoc/>
        public Task<bool> IsReadyAsync(EventRegistration reg,
                                       EventConsumerRegistration ecr,
                                       CancellationToken cancellationToken = default)
            => Task.FromResult(InternalIsReady(allowed: ecr.ReadinessTags));

        /// <inheritdoc/>
        public async Task WaitReadyAsync(CancellationToken cancellationToken = default)
        {
            // If disabled, do not proceed
            if (!options.Enabled) return;

            // Perform readiness check before starting bus.
            var timeout = options.Timeout;
            logger.ReadinessCheck(timeout);
            using var cts_timeout = new CancellationTokenSource(timeout);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts_timeout.Token);
            var ready = false;
            try
            {
                do
                {
                    ready = InternalIsReady(allowed: null /* no filters */);
                    if (!ready)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cts.Token); // delay for a second
                    }
                } while (!ready);
            }
            catch (TaskCanceledException)
            {
                logger.ReadinessCheckTimedout(timeout);
                throw;
            }
        }

        private bool InternalIsReady(ICollection<string>? allowed)
        {
            // If disabled, do not proceed
            if (!options.Enabled)
            {
                logger.ReadinessCheckDisabled();
                return true;
            }

            // if the health report is not set, we are not ready.
            if (healthReport is null) return false;

            // if the report already says healthy, there is no need to proceed
            if (healthReport.Status == HealthStatus.Healthy) return true;

            // at this point, we are not healthy but what we care about might be healthy
            // so we filter by tags
            var entries = healthReport.Entries.Select(kvp => kvp.Value).Where(e => ShouldInclude(e, allowed));
            return entries.All(e => e.Status == HealthStatus.Healthy);
        }

        private static bool ShouldInclude(HealthReportEntry entry, ICollection<string>? allowed)
        {
            var r_tags = entry.Tags ?? Array.Empty<string>();

            // Exclude the bus if configured to do so
            if (r_tags.Contains("eventbus")) return false;

            /*
             * If allowed is null, means there is no filtering so we include the registration.
             * Otherwise, check if any of the allowed tags is present in the registration's tags.
             * If so, we should include the registration
             */
            return allowed == null || allowed.Intersect(r_tags, StringComparer.OrdinalIgnoreCase).Any();
        }

        Task IHealthCheckPublisher.PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref healthReport, report);
            return Task.CompletedTask;
        }
    }
}
