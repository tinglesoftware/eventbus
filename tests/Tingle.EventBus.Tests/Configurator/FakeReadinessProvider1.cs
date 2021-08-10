using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Readiness;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Tests.Configurator
{
    public class FakeReadinessProvider1 : IReadinessProvider
    {
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> IsReadyAsync(EventRegistration ereg,
                                       EventConsumerRegistration creg,
                                       CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task WaitReadyAsync(CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
