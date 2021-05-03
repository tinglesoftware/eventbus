using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Readiness;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Tests
{
    public class FakeReadinessProvider1 : IReadinessProvider
    {
        public Task<bool> IsReadyAsync(EventRegistration ereg = null,
                                       EventConsumerRegistration creg = null,
                                       CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
