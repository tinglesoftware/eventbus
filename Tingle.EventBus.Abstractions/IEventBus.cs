using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public interface IEventBus : IEventBusPublisher
    {
        Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
    }
}
