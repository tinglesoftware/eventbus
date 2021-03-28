using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Transports
{
    /// <summary>
    /// The abstractions for an event bus
    /// </summary>
    /// <typeparam name="TTransportOptions">The type used for configuring options of the transport</typeparam>
    public abstract class EventBusTransportBase<TTransportOptions> : IEventBusTransport where TTransportOptions : EventBusTransportOptionsBase, new()
    {
        private static readonly Regex CategoryNamePattern = new Regex(@"Transport$", RegexOptions.Compiled);
        private readonly IServiceScopeFactory serviceScopeFactory;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public EventBusTransportBase(IServiceScopeFactory serviceScopeFactory,
                                     IOptions<EventBusOptions> busOptionsAccessor,
                                     IOptions<TTransportOptions> transportOptionsAccessor,
                                     ILoggerFactory loggerFactory)
        {
            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            BusOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
            TransportOptions = transportOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(transportOptionsAccessor));

            // Create a well-scoped logger
            var categoryName = $"{LogCategoryNames.Transports}.{GetType().Name}";
            categoryName = CategoryNamePattern.Replace(categoryName, string.Empty); // remove trailing "Transport"
            Logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));

            // Get the name of the transport
            Name = EventBusBuilder.GetTransportName(GetType());
        }

        /// <summary>
        /// Options for configuring the bus.
        /// </summary>
        protected EventBusOptions BusOptions { get; }

        /// <summary>
        /// Options for configuring the transport.
        /// </summary>
        protected TTransportOptions TransportOptions { get; }

        /// <inheritdoc/>
        public string Name { get; }

        ///
        protected ILogger Logger { get; }

        /// <inheritdoc/>
        public abstract Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                    CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                          EventRegistration registration,
                                                          DateTimeOffset? scheduled = null,
                                                          CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public abstract Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                 EventRegistration registration,
                                                                 DateTimeOffset? scheduled = null,
                                                                 CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public abstract Task CancelAsync<TEvent>(string id,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public abstract Task CancelAsync<TEvent>(IList<string> ids,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <inheritdoc/>
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.StartingTransport(GetRegistrationsCount(), TransportOptions.EmptyResultsDelay);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.StoppingTransport();
            return Task.CompletedTask;
        }

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
            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Deserialize);
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);

            // Get the serializer
            var serializer = (IEventSerializer)scope.ServiceProvider.GetRequiredService(registration.EventSerializerType);
            activity?.AddTag(ActivityTagNames.EventBusSerializerType, serializer.GetType().FullName);

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
        protected async Task SerializeAsync<TEvent>(Stream body,
                                                    EventContext<TEvent> @event,
                                                    EventRegistration registration,
                                                    IServiceScope scope,
                                                    CancellationToken cancellationToken = default)
            where TEvent : class
        {
            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Serialize);
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);

            // Get the serializer
            var serializer = (IEventSerializer)scope.ServiceProvider.GetRequiredService(registration.EventSerializerType);
            activity?.AddTag(ActivityTagNames.EventBusSerializerType, serializer.GetType().FullName);

            // do actual serialization and return the content type
            await serializer.SerializeAsync(body, @event, BusOptions.HostInfo, cancellationToken);
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
            where TConsumer : IEventConsumer<TEvent>
        {
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

        #region Registrations

        /// <summary>
        /// Gets the consumer registrations for this transport.
        /// </summary>
        /// <returns></returns>
        protected ICollection<EventRegistration> GetRegistrations() => BusOptions.GetRegistrations(transportName: Name);

        /// <summary>
        /// Gets the number of consumer registrations for this transport.
        /// </summary>
        /// <returns></returns>
        protected int GetRegistrationsCount() => BusOptions.GetRegistrationsCount(transportName: Name);

        #endregion

        #region Logging
        /// <summary>
        /// Begins a logical operation scope for logging.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="correlationId"></param>
        /// <param name="sequenceNumber"></param>
        /// <param name="extras">The extras to put in the scope. (Optional)</param>
        /// <returns>A disposable object that ends the logical operation scope on dispose.</returns>
        protected IDisposable BeginLoggingScopeForConsume(string id,
                                                          string correlationId,
                                                          string sequenceNumber = null,
                                                          IDictionary<string, string> extras = null)
        {
            var state = new Dictionary<string, string>();
            state.AddIfNotDefault(AttributeNames.Id, id);
            state.AddIfNotDefault(AttributeNames.CorrelationId, correlationId);
            state.AddIfNotDefault(AttributeNames.SequenceNumber, sequenceNumber);

            // if there are extras, add them
            if (extras != null)
            {
                foreach (var kvp in extras)
                {
                    state.AddIfNotDefault(kvp.Key, kvp.Value);
                }
            }

            // create the scope
            return Logger.BeginScope(state);
        }

        /// <summary>
        /// Begins a logical operation scope for logging.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="correlationId"></param>
        /// <param name="sequenceNumber"></param>
        /// <param name="extras">The extras to put in the scope. (Optional)</param>
        /// <returns>A disposable object that ends the logical operation scope on dispose.</returns>
        protected IDisposable BeginLoggingScopeForConsume(string id,
                                                          string correlationId,
                                                          long sequenceNumber,
                                                          IDictionary<string, string> extras = null)
        {
            return BeginLoggingScopeForConsume(id: id,
                                               correlationId: correlationId,
                                               sequenceNumber: sequenceNumber.ToString(),
                                               extras: extras);
        }

        #endregion
    }
}
