using Tingle.EventBus.Configuration;
using Xunit;

namespace Tingle.EventBus.Transports.Azure.ServiceBus.Tests;

public class AzureServiceBusTransportTests
{
    [Theory]
    [InlineData(false, null, false, PostConsumeAction.Abandon)]
    [InlineData(false, null, true, PostConsumeAction.Throw)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Deadletter, false, PostConsumeAction.Deadletter)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Deadletter, true, PostConsumeAction.Throw)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Discard, false, PostConsumeAction.Complete)]
    [InlineData(false, UnhandledConsumerErrorBehaviour.Discard, true, null)]
    [InlineData(true, null, false, PostConsumeAction.Complete)]
    [InlineData(true, null, true, null)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Deadletter, false, PostConsumeAction.Complete)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Deadletter, true, null)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Discard, false, PostConsumeAction.Complete)]
    [InlineData(true, UnhandledConsumerErrorBehaviour.Discard, true, null)]
    internal void DecideAction_Works(bool successful, UnhandledConsumerErrorBehaviour? behaviour, bool autoComplete, PostConsumeAction? expected)
    {
        var actual = AzureServiceBusTransport.DecideAction(successful: successful,
                                                           behaviour: behaviour,
                                                           autoComplete: autoComplete);

        Assert.Equal(expected, actual);
    }
}
