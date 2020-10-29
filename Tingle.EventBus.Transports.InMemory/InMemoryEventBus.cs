using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            @event.Headers ??= new EventHeaders();
            @event.Headers.MessageId ??= Guid.NewGuid().ToString();

            var scheduledId = scheduled?.ToUnixTimeMilliseconds().ToString();
            published.Add(@event);
            var _ = SendToConsumersAsync(@event, scheduled, CancellationToken.None);
            return Task.FromResult(scheduledId);
        }

        public override Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task SendToConsumersAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled, CancellationToken cancellationToken)
        {
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
            var tasks = registered.Select(reg => DispatchToConsumerAsync(reg, @event, cancellationToken)).ToList();
            await Task.WhenAll(tasks);
        }

        private async Task DispatchToConsumerAsync<TEvent>(EventConsumerRegistration reg, EventContext<TEvent> @event, CancellationToken cancellationToken)
        {
            // get the method to invoke
            var consumerType = reg.ConsumerType;
            var contextType = typeof(EventContext<>).MakeGenericType(reg.EventType);
            var method = consumerType.GetMethod(ConsumeMethodName);

            // resolve the consumer
            using var scope = serviceScopeFactory.CreateScope();
            var provider = scope.ServiceProvider;
            var consumer = provider.GetRequiredService(consumerType);

            var context = new EventContext<TEvent>
            {
                Headers = new EventHeaders
                {
                    CorrelationId = @event.Headers.CorrelationId,
                    MessageId = Guid.NewGuid().ToString(),
                },
                Event = @event.Event,
            };
            context.SetBus(this);

            try
            {
                var tsk = (Task)method.Invoke(consumer, new object[] { @event, cancellationToken, });
                await tsk.ConfigureAwait(false);

                consumed.Add(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing failed. Moving to deadletter.");
                failed.Add(contextType);
            }
        }
    }
}
