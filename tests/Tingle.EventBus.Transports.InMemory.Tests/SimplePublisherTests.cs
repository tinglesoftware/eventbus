using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tingle.EventBus.Transports.InMemory;

namespace Tingle.EventBus.Tests.InMemory;

public class SimplePublisherTests
{
    // This unit test applies when you have an implementation that does not call the event publisher directly

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public async Task EventIsPublishedOnBusAsync(int orderNumber)
    {
        var host = Host.CreateDefaultBuilder()
                       .ConfigureServices((context, services) =>
                       {
                           services.AddSingleton<RandomOrderProcessor>();
                           services.AddEventBus(builder =>
                           {
                               builder.AddInMemoryTransport();
                               builder.AddInMemoryTestHarness();
                           });
                       })
                       .Build();

        var provider = host.Services;
        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        await harness.StartAsync();
        try
        {
            var orderProcessor = provider.GetRequiredService<RandomOrderProcessor>();
            await orderProcessor.ProcessAsync(orderNumber);

            // Ensure no failures
            Assert.False(harness.Failed().Any());

            // expect publish for event order numbers
            if ((orderNumber % 2) == 0)
            {
                // Ensure only one was published
                var context = Assert.Single(harness.Published<OrderProcessedEvent>());
                var evt = context.Event;
                Assert.Equal(2021 + orderNumber, evt.Year);
            }
            else
            {
                // Ensure nothing was published
                Assert.False(harness.Published().Any());
            }
        }
        finally
        {
            await harness.StopAsync();
        }
    }

    class OrderProcessedEvent : SimpleConsumer.SampleEvent { }

    class RandomOrderProcessor(IEventPublisher publisher)
    {
        public async Task ProcessAsync(int orderNumber)
        {
            // only publish if the order number is even (can be any other condition)
            if ((orderNumber % 2) == 0)
            {
                await publisher.PublishAsync(new OrderProcessedEvent
                {
                    Make = "TESLA",
                    Model = "Roadster 2.0",
                    Registration = "1234567890",
                    VIN = "5YJ3E1EA5KF328931",
                    Year = 2021 + orderNumber,
                });
            }
        }
    }
}
