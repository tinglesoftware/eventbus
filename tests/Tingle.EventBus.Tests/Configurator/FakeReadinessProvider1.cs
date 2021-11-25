using Tingle.EventBus.Configuration;
using Tingle.EventBus.Readiness;

namespace Tingle.EventBus.Tests.Configurator;

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
