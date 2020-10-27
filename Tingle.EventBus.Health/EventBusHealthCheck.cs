using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tingle.EventBus.Abstractions;

namespace Tingle.EventBus.Health
{
    /// <summary>
    /// Implementation of <see cref="IHealthCheck"/> for an <see cref="IEventBus"/>
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    public class EventBusHealthCheck : IHealthCheck
    {
        private readonly IEventBus eventBus;

        /// <summary>
        /// Creates an instance of <see cref="EventBusHealthCheck"/>
        /// </summary>
        /// <param name="eventBus">The instance of <see cref="IEventBus"/> to use.</param>
        public EventBusHealthCheck(IEventBus eventBus)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                _ = await eventBus.CheckHealthAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
            }
        }
    }
}
