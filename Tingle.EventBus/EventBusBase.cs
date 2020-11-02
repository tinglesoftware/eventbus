using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus
{
    public abstract class EventBusBase : IEventBus
    {
        protected static readonly DiagnosticListener DiagnosticListener = new DiagnosticListener("Tingle-EventBus");

        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly ILogger logger;

        public EventBusBase(IHostEnvironment environment,
                            IServiceScopeFactory serviceScopeFactory,
                            IOptions<EventBusOptions> optionsAccessor,
                            ILoggerFactory loggerFactory)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            Options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
            logger = loggerFactory?.CreateLogger("EventBus") ?? throw new ArgumentNullException(nameof(logger));
        }

        protected IHostEnvironment Environment { get; }
        protected EventBusOptions Options { get; }

        /// <inheritdoc/>
        public abstract Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                          DateTimeOffset? scheduled = null,
                                                          CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public abstract Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                 DateTimeOffset? scheduled = null,
                                                                 CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public abstract Task StartAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public abstract Task StopAsync(CancellationToken cancellationToken);

        protected async Task<EventContext<TEvent>> DeserializeAsync<TEvent>(Stream body, ContentType contentType, CancellationToken cancellationToken)
            where TEvent : class
        {
            // Get the serializer. Should we find a serializer based on the content type?
            using var scope = serviceScopeFactory.CreateScope();
            var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();

            // Deserialize the content into a context
            return await serializer.DeserializeAsync<TEvent>(body, cancellationToken);
        }

        protected async Task<ContentType> SerializeAsync<TEvent>(Stream body, EventContext<TEvent> @event, CancellationToken cancellationToken)
            where TEvent : class
        {
            // set properties that may be missing
            @event.EventId ??= Guid.NewGuid().ToString();
            @event.Sent ??= DateTimeOffset.UtcNow;

            // Get the serializer. Should we find a serializer based on the content type?
            using var scope = serviceScopeFactory.CreateScope();
            var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();

            // do actual serialization
            await serializer.SerializeAsync(body, @event, Options.HostInfo, cancellationToken);

            // return the content type written
            return serializer.ContentType;
        }

        protected async Task PushToConsumerAsync<TEvent, TConsumer>(EventContext<TEvent> eventContext, CancellationToken cancellationToken)
            where TConsumer : IEventBusConsumer<TEvent>
        {
            // resolve the consumer
            using var scope = serviceScopeFactory.CreateScope();
            var provider = scope.ServiceProvider;
            var consumer = provider.GetRequiredService<TConsumer>();

            // set the bus
            eventContext.SetBus(this);

            // invoke handler method
            await consumer.ConsumeAsync(eventContext, cancellationToken).ConfigureAwait(false);
        }
    }
}
