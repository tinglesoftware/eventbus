using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tingle.EventBus.Transports.InMemory;

namespace Tingle.EventBus.Tests.InMemory;

public class SimpleCancellationTests
{
    [Fact]
    public async Task EventIsPublishedOnBusAsync()
    {
        var host = Host.CreateDefaultBuilder()
                       .ConfigureServices((context, services) =>
                       {
                           services.AddEventBus(builder =>
                           {
                               builder.AddInMemoryTransport();
                               builder.AddInMemoryTestHarness();
                           });
                       })
                       .Build();

        var provider = host.Services;
        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        await harness.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var publisher = provider.GetRequiredService<IEventPublisher>();
            var evt = new DoorOpenedEvent
            {
                Make = "TESLA",
                Model = "Roadster 2.0",
                Registration = "1234567890",
                VIN = "5YJ3E1EA5KF328931",
                Year = 2021,
            };
            var schedulingId = (string?)await publisher.PublishAsync(@event: evt,
                                                                     scheduled: DateTimeOffset.UtcNow.AddDays(1),
                                                                     cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(schedulingId);

            // Ensure no failures
            Assert.False(harness.Failed().Any());

            // Ensure only one was published
            Assert.Single(harness.Published<DoorOpenedEvent>());

            await publisher.CancelAsync<DoorOpenedEvent>(schedulingId, TestContext.Current.CancellationToken);

            // Ensure only one was cancelled
            var sn = Assert.Single(harness.Cancelled());
            Assert.Equal(schedulingId, sn.ToString());
        }
        finally
        {
            await harness.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    class DoorOpenedEvent : SimpleConsumer.SampleEvent { }
}
