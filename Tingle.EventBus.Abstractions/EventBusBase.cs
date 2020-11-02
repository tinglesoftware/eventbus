using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public abstract class EventBusBase : IEventBus
    {
        protected static readonly DiagnosticListener DiagnosticListener = new DiagnosticListener("Tingle-EventBus");

        private static readonly Regex namePattern = new Regex("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);

        private readonly IServiceScopeFactory serviceScopeFactory;
        protected readonly IEventSerializer eventSerializer;
        private readonly ConcurrentDictionary<Type, string> typeNamesCache = new ConcurrentDictionary<Type, string>();
        private readonly ILogger logger;

        public EventBusBase(IHostEnvironment environment,
                            IServiceScopeFactory serviceScopeFactory,
                            IEventSerializer eventSerializer,
                            IOptions<EventBusOptions> optionsAccessor,
                            ILoggerFactory loggerFactory)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            this.eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
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

        /// <summary>
        /// Get's the event name for a given type.
        /// </summary>
        /// <param name="type"></param>
        protected virtual string GetEventName(Type type) => typeNamesCache.GetOrAdd(type, CreateEventName(type));

        /// <summary>
        /// Get's the event name for a given type.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <returns></returns>
        protected virtual string GetEventName<TEvent>() => GetEventName(typeof(TEvent));

        private string CreateEventName(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            var name = Options.UseFullTypeNames ? type.FullName : type.Name;
            return ApplyNamingConvention(name, Options.NamingConvention);
        }

        /// <summary>
        /// Get's the consumer name for a given type.
        /// </summary>
        /// <param name="type"></param>
        protected virtual string GetConsumerName(Type type, bool forceConsumerName)
        {
            return typeNamesCache.GetOrAdd(type, CreateConsumerName(type, forceConsumerName));
        }

        private string CreateConsumerName(Type type, bool forceConsumerName)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            // for consumers, we always enforce the full type name
            var name = (Options.UseApplicationNameInsteadOfConsumerName && !forceConsumerName)
                        ? Environment.ApplicationName
                        : type.FullName;
            return ApplyNamingConvention(name, Options.NamingConvention);
        }

        private static string ApplyNamingConvention(string name, EventBusNamingConvention convention)
        {
            return convention switch
            {
                EventBusNamingConvention.KebabCase => namePattern.Replace(name, m => "-" + m.Value).ToLowerInvariant(),
                EventBusNamingConvention.SnakeCase => namePattern.Replace(name, m => "_" + m.Value).ToLowerInvariant(),
                _ => name,
            };
        }

        protected async Task<EventContext<TEvent>> DeserializeAsync<TEvent>(Stream body, CancellationToken cancellationToken)
            where TEvent : class
        {
            var context = await eventSerializer.DeserializeAsync<TEvent>(body, cancellationToken);
            return context;
        }

        protected async Task SerializeAsync<TEvent>(Stream body, EventContext<TEvent> @event, CancellationToken cancellationToken)
            where TEvent : class
        {
            await eventSerializer.SerializeAsync<TEvent>(body, @event, cancellationToken);
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
