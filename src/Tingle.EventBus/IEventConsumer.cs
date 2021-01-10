using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    /// <summary>
    /// Contract describing a consumer of one or more events.
    /// </summary>
    public interface IEventConsumer
    {
        // Intentionally left blank
    }

    /// <summary>
    /// Contract describing a consumer of an event.
    /// </summary>
    public interface IEventConsumer<T> : IEventConsumer
    {
        /// <summary>
        /// Consume an event of the provided type.
        /// </summary>
        /// <param name="context">The context of the event</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ConsumeAsync(EventContext<T> context, CancellationToken cancellationToken);
    }
}
