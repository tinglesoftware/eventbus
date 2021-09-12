using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Readiness
{
    /// <summary>
    /// Provider for checking readiness.
    /// </summary>
    public interface IReadinessProvider
    {
        /// <summary>
        /// Check if the application is ready for the bus to be started.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a consumer is ready to receives events.
        /// </summary>
        /// <param name="reg">The <see cref="EventRegistration"/> that the consumer belongs to.</param>
        /// <param name="ecr">The <see cref="EventConsumerRegistration"/> for the consumer.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> IsReadyAsync(EventRegistration reg,
                                EventConsumerRegistration ecr,
                                CancellationToken cancellationToken = default);

        /// <summary>
        /// Waits for the application to be ready before returning.
        /// The implementation determines how long to wait before being ready.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task WaitReadyAsync(CancellationToken cancellationToken = default);
    }
}
