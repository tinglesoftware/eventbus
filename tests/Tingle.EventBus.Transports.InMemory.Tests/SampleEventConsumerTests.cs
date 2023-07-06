using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleConsumer;
using Tingle.EventBus.Transports.InMemory;
using Xunit.Abstractions;

namespace Tingle.EventBus.Tests.InMemory;

public class SampleEventConsumerTests
{
    private readonly ITestOutputHelper outputHelper;

    public SampleEventConsumerTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Fact]
    public async Task ConsumerWorksAsync()
    {
        var counter = new EventCounter();
        var host = Host.CreateDefaultBuilder()
                       .ConfigureLogging((context, builder) => builder.AddXUnit(outputHelper))
                       .ConfigureServices((context, services) =>
                       {
                           services.AddSingleton(counter);
                           services.AddEventBus(builder =>
                           {
                               builder.AddConsumer<SampleEventConsumer>();
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
            // Ensure we start at 0 for the counter
            Assert.Equal(0, counter.Count);

            // Get the publisher and publish the event
            var publisher = provider.GetRequiredService<IEventPublisher>();
            await publisher.PublishAsync(new SampleEvent
            {
                Make = "TESLA",
                Model = "Roadster 2.0",
                Registration = "1234567890",
                VIN = "5YJ3E1EA5KF328931",
                Year = 2021
            });

            // Ensure no faults were published by the consumer
            Assert.False(harness.Failed<SampleEvent>().Any());

            // Ensure the message was consumed
            Assert.NotEmpty(await harness.ConsumedAsync<SampleEvent>(TimeSpan.FromSeconds(0.5f)));

            // Now you can ensure data saved to database correctly

            // For this example, we test if the counter was incremented from 0 to 1
            Assert.Equal(1, counter.Count);
        }
        finally
        {
            await harness.StopAsync();
        }
    }
}
