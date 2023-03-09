using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Transports.Kafka.Tests;

public class KafkaTransportTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Deadletter, true)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Discard, true)]
    [InlineData(false, null, false)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Deadletter, true)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Discard, true)]
    public void CanCheckpoint_Works(bool successful, UnhandledConsumerErrorBehaviour? behaviour, bool expected)
    {
        var actual = KafkaTransport.CanCheckpoint(successful, behaviour);
        Assert.Equal(expected, actual);
    }
}
