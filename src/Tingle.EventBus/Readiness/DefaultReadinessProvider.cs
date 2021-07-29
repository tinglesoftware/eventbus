using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
            => InternalIsReadyAsync(allowed: null /* no filters */, cancellationToken: cancellationToken);

        /// <inheritdoc/>
        public Task<bool> IsReadyAsync(EventRegistration ereg,
                                       EventConsumerRegistration creg,
                                       CancellationToken cancellationToken = default)
            => InternalIsReadyAsync(allowed: creg.ReadinessTags, cancellationToken: cancellationToken);

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
            var ct = cts.Token;
            var ready = false;
            try
            {
                do
                {
                    ready = await InternalIsReadyAsync(allowed: null /* no filters */, cancellationToken: ct);
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

        private async Task<bool> InternalIsReadyAsync(ICollection<string>? allowed, CancellationToken cancellationToken)
        {
            // If disabled, do not proceed
            if (!options.Enabled)
            {
                logger.ReadinessCheckDisabled();
                return true;
            }

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
                bool predicate(HealthCheckRegistration r) => ShouldInclude(registration: r, excludeSelf: options.ExcludeSelf, allowed: allowed);
                var report = await hcs.CheckHealthAsync(predicate: predicate, cancellationToken: cancellationToken);
                return report.Status == HealthStatus.Healthy;
            }

            return true;
        }

        private static bool ShouldInclude(HealthCheckRegistration registration, bool excludeSelf, ICollection<string>? allowed)
        {
            if (registration is null) throw new ArgumentNullException(nameof(registration));

            var r_tags = registration.Tags?.ToList() ?? new List<string>();

            // Exclude the bus if configured to do so
            if (excludeSelf && r_tags.Contains("eventbus")) return false;

            /*
             * If allowed is null, means there is no filtering so we include the registration.
             * Otherwise, check if any of the allowed tags is present in the registration's tags.
             * If so, we should include the registration
             */
            return allowed == null || allowed.Intersect(r_tags, StringComparer.OrdinalIgnoreCase).Any();
        }
    }
}
