using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Tests
{
    [ConsumerName("sample-consumer")]
    internal class TestConsumer2 : IEventConsumer<TestEvent2>
    {
        public Task ConsumeAsync(EventContext<TestEvent2> context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
