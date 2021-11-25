using Tingle.EventBus.Configuration;
using Xunit;

namespace Tingle.EventBus.Transports.Azure.EventHubs.Tests;

public class AzureEventHubsTransportTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Deadletter, true)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Discard, true)]
    [InlineData(false, null, false)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Deadletter, true)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Discard, true)]
    public void ShouldCheckpoint_Works(bool successful, UnhandledConsumerErrorBehaviour? behaviour, bool expected)
    {
        var actual = AzureEventHubsTransport.ShouldCheckpoint(successful, behaviour);
        Assert.Equal(expected, actual);
    }
}
