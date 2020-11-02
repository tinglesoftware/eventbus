using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    public interface IEventBusConsumer
    {
        // Intentionally left blank
    }

    public interface IEventBusConsumer<T> : IEventBusConsumer
    {
        Task ConsumeAsync(EventContext<T> context, CancellationToken cancellationToken = default);
    }
}
