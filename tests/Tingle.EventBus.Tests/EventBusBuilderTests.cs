using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Tests.Configurator;
using Tingle.EventBus.Transports;

namespace Tingle.EventBus.Tests;

public class EventBusBuilderTests
{
    [Fact]
    public void DeadletterConsumerIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("app1"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddEventBus(builder =>
        {
            builder.AddConsumer<DummyConsumer>();
            builder.Configure(o => o.AddTransport<DummyTransport>("Dummy", null));
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EventBusOptions>>().Value;
        Assert.Equal(2, options.Registrations.Count);

        var reg = Assert.Contains(typeof(TestEvent1), (IDictionary<Type, EventRegistration>)options.Registrations);
        var ecr = Assert.Single(reg.Consumers).Value;
        Assert.Equal(typeof(DummyConsumer), ecr.ConsumerType);
        Assert.False(ecr.Deadletter);

        reg = Assert.Contains(typeof(TestEvent2), (IDictionary<Type, EventRegistration>)options.Registrations);
        ecr = Assert.Single(reg.Consumers).Value;
        Assert.Equal(typeof(DummyConsumer), ecr.ConsumerType);
        Assert.True(ecr.Deadletter);
    }

    internal class DummyConsumer : IEventConsumer<TestEvent1>, IDeadLetteredEventConsumer<TestEvent2>
    {
        public Task ConsumeAsync(EventContext<TestEvent1> context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        public Task ConsumeAsync(DeadLetteredEventContext<TestEvent2> context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    internal class DummyTransport : IEventBusTransport
    {
        public string Name => throw new NotImplementedException();

        public Task CancelAsync<TEvent>(string id, EventRegistration registration, CancellationToken cancellationToken = default) where TEvent : class
        {
            throw new NotImplementedException();
        }

        public Task CancelAsync<TEvent>(IList<string> ids, EventRegistration registration, CancellationToken cancellationToken = default) where TEvent : class
        {
            throw new NotImplementedException();
        }

        public EventBusTransportOptions? GetOptions()
        {
            throw new NotImplementedException();
        }

        public void Initialize(EventBusTransportRegistration registration)
        {
            throw new NotImplementedException();
        }

        public Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event, EventRegistration registration, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default) where TEvent : class
        {
            throw new NotImplementedException();
        }

        public Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events, EventRegistration registration, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default) where TEvent : class
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
