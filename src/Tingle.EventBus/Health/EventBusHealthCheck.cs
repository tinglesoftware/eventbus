using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Health
{
    /// <summary>
    /// Implementation of <see cref="IHealthCheck"/> for <see cref="EventBus"/>
    /// </summary>
    public class EventBusHealthCheck : IHealthCheck
    {
        private readonly EventBus bus;

        /// <summary>
        /// Creates an instance of <see cref="EventBusHealthCheck"/>
        /// </summary>
        /// <param name="bus">The instance of <see cref="EventBus"/> to use.</param>
        public EventBusHealthCheck(EventBus bus)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var extras = new EventBusHealthCheckExtras();
                var healthy = await bus.CheckHealthAsync(extras, cancellationToken);
                return healthy ? HealthCheckResult.Healthy(description: extras.Description,
                                                           data: extras.Data)
                               : HealthCheckResult.Unhealthy(description: extras.Description,
                                                             data: extras.Data);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
            }
        }
    }
}
