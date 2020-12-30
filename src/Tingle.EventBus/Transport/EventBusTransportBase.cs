using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Transport
{
    /// <summary>
    /// The abstractions for an event bus
    /// </summary>
    /// <typeparam name="TTransportOptions">The type used for configuring options of the transport</typeparam>
    public abstract class EventBusTransportBase<TTransportOptions> : IEventBusTransport where TTransportOptions : class, new()
    {
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public EventBusTransportBase(IHostEnvironment environment,
                                     IServiceScopeFactory serviceScopeFactory,
                                     IOptions<EventBusOptions> busOptionsAccessor,
                                     IOptions<TTransportOptions> transportOptionsAccessor,
                                     ILoggerFactory loggerFactory)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            BusOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
            TransportOptions = transportOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(transportOptionsAccessor));
            logger = loggerFactory?.CreateLogger("EventBus") ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// The environment in which the application and by extension the bus is running in.
        /// </summary>
        protected IHostEnvironment Environment { get; }

        /// <summary>
        /// Options for configuring the bus.
        /// </summary>
        protected EventBusOptions BusOptions { get; }

        /// <summary>
        /// Options for configuring the transport.
        /// </summary>
        protected TTransportOptions TransportOptions { get; }

        /// <inheritdoc/>
        public abstract Task<bool> CheckHealthAsync(EventBusHealthCheckExtras extras,
                                                    CancellationToken cancellationToken = default);

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
        public abstract Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public abstract Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public abstract Task StartAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public abstract Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Deserialize an event from a stream of bytes.
        /// </summary>
        /// <typeparam name="TEvent">The event type to be deserialized.</typeparam>
        /// <param name="body">
        /// The <see cref="Stream"/> containing the raw data.
        /// (It must be readable, i.e. <see cref="Stream.CanRead"/> must be true).
        /// </param>
        /// <param name="contentType">The type of content contained in the <paramref name="body"/>.</param>
        /// <param name="registration">The bus registration for this event.</param>
        /// <param name="scope">The scope in which to resolve required services.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected async Task<EventContext<TEvent>> DeserializeAsync<TEvent>(Stream body,
                                                                            ContentType contentType,
                                                                            EventRegistration registration,
                                                                            IServiceScope scope,
                                                                            CancellationToken cancellationToken = default)
            where TEvent : class
        {
            // Get the serializer
            var serializer = (IEventSerializer)scope.ServiceProvider.GetRequiredService(registration.EventSerializerType);

            // Deserialize the content into a context
            return await serializer.DeserializeAsync<TEvent>(body, contentType, cancellationToken);
        }

        /// <summary>
        /// Serialize an event into a stream of bytes.
        /// </summary>
        /// <typeparam name="TEvent">The event type to be serialized.</typeparam>
        /// <param name="body">
        /// The stream to serialize to.
        /// (It must be writeable, i.e. <see cref="Stream.CanWrite"/> must be true).
        /// </param>
        /// <param name="event">The context of the event to be serialized.</param>
        /// <param name="registration">The bus registration for this event.</param>
        /// <param name="scope">The scope in which to resolve required services.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected async Task<ContentType> SerializeAsync<TEvent>(Stream body,
                                                                 EventContext<TEvent> @event,
                                                                 EventRegistration registration,
                                                                 IServiceScope scope,
                                                                 CancellationToken cancellationToken = default)
            where TEvent : class
        {
            // set properties that may be missing
            @event.EventId ??= Guid.NewGuid().ToString();
            @event.Sent ??= DateTimeOffset.UtcNow;

            // Get the serializer
            var serializer = (IEventSerializer)scope.ServiceProvider.GetRequiredService(registration.EventSerializerType);

            // do actual serialization and return the content type
            return await serializer.SerializeAsync(body, @event, BusOptions.HostInfo, cancellationToken);
        }

        /// <summary>
        /// Push an incoming event to the consumer responsible for it.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <typeparam name="TConsumer">The type of consumer</typeparam>
        /// <param name="event">The context containing the event</param>
        /// <param name="scope">The scope in which to resolve required services.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected async Task ConsumeAsync<TEvent, TConsumer>(EventContext<TEvent> @event,
                                                             IServiceScope scope,
                                                             CancellationToken cancellationToken)
            where TConsumer : IEventBusConsumer<TEvent>
        {
            // Set the bus in context
            @event.SetBus(this);

            // Resolve the consumer
            var consumer = scope.ServiceProvider.GetRequiredService<TConsumer>();

            // Invoke handler method
            await consumer.ConsumeAsync(@event, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Create an <see cref="IServiceScope"/> which contains an <see cref="IServiceProvider"/>
        /// used to resolve dependencies from a newly created scope.
        /// </summary>
        /// <returns>
        /// An <see cref="IServiceScope"/> controlling the lifetime of the scope.
        /// Once this is disposed, any scoped services that have been resolved
        /// from the <see cref="IServiceScope.ServiceProvider"/> will also be disposed.
        /// </returns>
        protected IServiceScope CreateScope() => serviceScopeFactory.CreateScope();
    }
}
