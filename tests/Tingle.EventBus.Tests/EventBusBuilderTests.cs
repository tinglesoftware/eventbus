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
    public void DeadletterConsumerIsRegistered_DifferentEventTypes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("app1"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddEventBus(builder =>
        {
            builder.AddConsumer<DummyConsumer1>();
            builder.Configure(o => o.AddTransport<DummyTransport>("Dummy", null));
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EventBusOptions>>().Value;
        Assert.Equal(2, options.Registrations.Count);

        var reg = Assert.Contains(typeof(TestEvent1), (IDictionary<Type, EventRegistration>)options.Registrations);
        var ecr = Assert.Single(reg.Consumers);
        Assert.Equal(typeof(DummyConsumer1), ecr.ConsumerType);
        Assert.False(ecr.Deadletter);

        reg = Assert.Contains(typeof(TestEvent2), (IDictionary<Type, EventRegistration>)options.Registrations);
        ecr = Assert.Single(reg.Consumers);
        Assert.Equal(typeof(DummyConsumer1), ecr.ConsumerType);
        Assert.True(ecr.Deadletter);
    }

    [Fact]
    public void DeadletterConsumerIsRegistered_SameEventType()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("app1"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddEventBus(builder =>
        {
            builder.AddConsumer<DummyConsumer2>();
            builder.Configure(o => o.AddTransport<DummyTransport>("Dummy", null));
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EventBusOptions>>().Value;
        var reg = Assert.Single(options.Registrations).Value;
        Assert.Equal(2, reg.Consumers.Count);
        Assert.All(reg.Consumers, ecr => Assert.Equal(typeof(DummyConsumer2), ecr.ConsumerType));
        Assert.False(reg.Consumers.ElementAt(0).Deadletter);
        Assert.True(reg.Consumers.ElementAt(1).Deadletter);
    }

    internal class DummyConsumer1 : IEventConsumer<TestEvent1>, IDeadLetteredEventConsumer<TestEvent2>
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

    internal class DummyConsumer2 : IEventConsumer<TestEvent2>, IDeadLetteredEventConsumer<TestEvent2>
    {
        public Task ConsumeAsync(EventContext<TestEvent2> context, CancellationToken cancellationToken = default)
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
