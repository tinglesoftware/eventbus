using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Abstractions;

namespace Tingle.EventBus.Transports.InMemory
{
    public class InMemoryEventBus : EventBusBase
    {
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly ConcurrentBag<object> published = new ConcurrentBag<object>();
        private readonly ConcurrentBag<object> consumed = new ConcurrentBag<object>();
        private readonly ConcurrentBag<object> failed = new ConcurrentBag<object>();
        private readonly ILogger logger;

        public InMemoryEventBus(IHostEnvironment environment,
                                IServiceScopeFactory serviceScopeFactory,
                                IOptions<EventBusOptions> optionsAccessor,
                                ILoggerFactory loggerFactory)
            : base(environment, optionsAccessor, loggerFactory)
        {
            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            logger = loggerFactory?.CreateLogger<InMemoryEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IEnumerable<object> Published => published;
        public IEnumerable<object> Consumed => consumed;
        public IEnumerable<object> Failed => failed;

        public override Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public override Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            @event.EventId ??= Guid.NewGuid().ToString();

            var scheduledId = scheduled?.ToUnixTimeMilliseconds().ToString();
            published.Add(@event);
            var _ = SendToConsumersAsync(@event, scheduled);
            return Task.FromResult(scheduledId);
        }

        public override Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task SendToConsumersAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled)
        {
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            // if the message is scheduled, apply a delay for the duration
            if (scheduled != null)
            {
                var remainder = DateTimeOffset.UtcNow - scheduled.Value;
                if (remainder > TimeSpan.Zero)
                {
                    await Task.Delay(remainder, cancellationToken);
                }
            }

            // find consumers registered for the event
            var eventType = typeof(TEvent);
            var registered = Options.GetRegistrations().Where(r => r.EventType == eventType).ToList();

            // send the message to each consumer in parallel
            var tasks = registered.Select(reg =>
            {
                var method =GetType().GetMethod(nameof(DispatchToConsumerAsync)).MakeGenericMethod(typeof(TEvent), reg.ConsumerType);
                return (Task)method.Invoke(this, new object[] { @event, cancellationToken, });
            }).ToList();
            await Task.WhenAll(tasks);
        }

        private async Task DispatchToConsumerAsync<TEvent, TConsumer>(EventContext<TEvent> @event, CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            // resolve the consumer
            using var scope = serviceScopeFactory.CreateScope();
            var provider = scope.ServiceProvider;
            var consumer = provider.GetRequiredService<TConsumer>();

            var context = new EventContext<TEvent>
            {
                EventId = Guid.NewGuid().ToString(),
                CorrelationId = @event.CorrelationId,
                Event = @event.Event,
            };
            context.SetBus(this);

            try
            {
                await consumer.ConsumeAsync(context, cancellationToken).ConfigureAwait(false);
                consumed.Add(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing failed. Moving to deadletter.");
                failed.Add(context);
            }
        }
    }
}
