using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;

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
        /// <param name="ereg">The <see cref="EventRegistration"/> that the consumer belongs to.</param>
        /// <param name="creg">The <see cref="EventConsumerRegistration"/> for the consumer.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> IsReadyAsync(EventRegistration ereg,
                                EventConsumerRegistration creg,
                                CancellationToken cancellationToken = default);
    }
}
