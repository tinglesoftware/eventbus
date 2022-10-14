using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Tests.Configurator;

[ConsumerName("sample-consumer")]
internal class TestConsumer2 : IEventConsumer<TestEvent2>
{
    public Task ConsumeAsync(EventContext<TestEvent2> context, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
