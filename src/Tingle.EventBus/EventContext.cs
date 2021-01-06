using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus
{
    /// <summary>
    /// Generic context for an event.
    /// </summary>
    public abstract class EventContext : IEventPublisher
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bus">The <see cref="EventBus"/> in which this context exists.</param>
        protected EventContext(EventBus bus) => Bus = bus ?? throw new ArgumentNullException(nameof(bus));

        /// <summary>
        /// The unique identifier of the event.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The unique identifier of the request accosiated with the event.
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// A value shared between related events.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// The unique identifier of the initiator of the event.
        /// </summary>
        public string InitiatorId { get; set; }

        /// <summary>
        /// The specific time at which the event expires.
        /// </summary>
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// The specific time the event was sent.
        /// </summary>
        public DateTimeOffset? Sent { get; set; }

        /// <summary>
        /// The headers published alongside the event.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// The content type used to serialize and deserialize the event to/from a stream of bytes.
        /// Setting a value, instructs the serializer how to write the event content on the transport.
        /// The serializer used for the event must support the value set.
        /// When set to <see langword="null"/>, the serializer used for the event decides what
        /// content type to use depending on its implementation.
        /// For the default implementation, see <see cref="Serialization.DefaultEventSerializer"/>.
        /// </summary>
        public ContentType ContentType { get; set; }

        /// <summary>
        /// The bus in which this context exists.
        /// </summary>
        internal EventBus Bus { get; }

        /// <inheritdoc/>
        public EventContext<TEvent> CreateEventContext<TEvent>(TEvent @event, string correlationId = null)
        {
            return new EventContext<TEvent>(Bus)
            {
                Event = @event,
                CorrelationId = correlationId ?? Id,
            };
        }

        /// <inheritdoc/>
        public Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                 DateTimeOffset? scheduled = null,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class
        {
            return Bus.PublishAsync(@event: @event, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                        DateTimeOffset? scheduled = null,
                                                        CancellationToken cancellationToken = default)
            where TEvent : class
        {
            return Bus.PublishAsync(events: events, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task<string> PublishAsync<TEvent>(TEvent @event,
                                                 DateTimeOffset? scheduled = null,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var context = CreateEventContext(@event);
            return PublishAsync(@event: context, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IList<string>> PublishAsync<TEvent>(IList<TEvent> events,
                                                        DateTimeOffset? scheduled = null,
                                                        CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var contexts = events.Select(e => CreateEventContext(e)).ToList();
            return PublishAsync(events: contexts, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default) where TEvent : class
        {
            await Bus.CancelAsync<TEvent>(id: id, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default) where TEvent : class
        {
            await Bus.CancelAsync<TEvent>(ids: ids, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// The context for a specific event.
    /// </summary>
    /// <typeparam name="T">The type of event carried.</typeparam>
    public class EventContext<T> : EventContext
    {
        /// <inheritdoc/>
        public EventContext(EventBus bus) : base(bus) { }

        /// <summary>
        /// The event published or to be published.
        /// </summary>
        public T Event { get; set; }
    }
}
