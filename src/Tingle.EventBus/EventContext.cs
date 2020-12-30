using System;
using System.Collections.Generic;
using System.Linq;
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
        public string EventId { get; set; }

        /// <summary>
        /// The unique identifier of the request accosiated with the event.
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// A value shared between related events.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// The unique identifier of the conversation.
        /// </summary>
        public string ConversationId { get; set; }

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
        /// The bus in which this context exists.
        /// </summary>
        internal EventBus Bus { get; }

        /// <inheritdoc/>
        public Task<string> PublishAsync<TEvent>(TEvent @event,
                                                 DateTimeOffset? scheduled = null,
                                                 CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var context = new EventContext<TEvent>(Bus)
            {
                CorrelationId = EventId,
                Event = @event,
            };

            return Bus.PublishAsync(@event: context, scheduled: scheduled, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IList<string>> PublishAsync<TEvent>(IList<TEvent> events,
                                                        DateTimeOffset? scheduled = null,
                                                        CancellationToken cancellationToken = default)
            where TEvent : class
        {
            var contexts = events.Select(e => new EventContext<TEvent>(Bus)
            {
                CorrelationId = EventId,
                Event = e,
            }).ToList();
            return Bus.PublishAsync(events: contexts, scheduled: scheduled, cancellationToken: cancellationToken);
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
