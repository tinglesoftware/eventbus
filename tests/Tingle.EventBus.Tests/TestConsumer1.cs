using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Tests
{
    internal class TestConsumer1 : IEventConsumer<TestEvent1>
    {
        public Task ConsumeAsync(EventContext<TestEvent1> context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
