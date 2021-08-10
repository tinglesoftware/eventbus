using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Tests.Configurator
{
    [ConsumerReadinessProvider(typeof(FakeReadinessProvider2))]
    internal class TestConsumer3 : IEventConsumer<TestEvent3>
    {
        public Task ConsumeAsync(EventContext<TestEvent3> context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
