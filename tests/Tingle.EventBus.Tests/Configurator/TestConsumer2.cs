using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Tests.Configurator
{
    [ConsumerName("sample-consumer")]
    [ConsumerReadinessProvider(typeof(FakeReadinessProvider1))]
    internal class TestConsumer2 : IEventConsumer<TestEvent2>
    {
        public Task ConsumeAsync(EventContext<TestEvent2> context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
