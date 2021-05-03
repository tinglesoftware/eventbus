using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Readiness;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Tests
{
    public class FakeReadinessProvider1 : IReadinessProvider
    {
        public Task<bool> ConsumerReadyAsync(EventRegistration ereg, EventConsumerRegistration creg, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
