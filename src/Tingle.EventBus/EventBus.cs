using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Transports;

namespace Tingle.EventBus
{
    /// <summary>
    /// The abstractions for an event bus
    /// </summary>
    public class EventBus : IHostedService
    {
        ///
        protected static readonly DiagnosticListener DiagnosticListener = new DiagnosticListener("Tingle-EventBus");

        private readonly IEnumerable<IEventBusTransport> transports;
        private readonly EventBusOptions options;
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="optionsAccessor"></param>
        /// <param name="transports"></param>
        /// <param name="loggerFactory"></param>
        public EventBus(IEnumerable<IEventBusTransport> transports,
                        IOptions<EventBusOptions> optionsAccessor,
                        ILoggerFactory loggerFactory)
        {
            this.transports = transports ?? throw new ArgumentNullException(nameof(transports));
            options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
            logger = loggerFactory?.CreateLogger("EventBus") ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks for health of the bus.
        /// This function can be used by the Health Checks framework and may throw and execption during execution.
        /// </summary>
        /// <param name="extras">The extra information about the health check.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A value indicating if the bus is healthly.</returns>
        public async Task<bool> CheckHealthAsync(EventBusHealthCheckExtras extras,
                                                 CancellationToken cancellationToken = default)
        {
            // Ensure each transport is healthy
            foreach (var t in transports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var healthy = await t.CheckHealthAsync(extras, cancellationToken);
                if (!healthy)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Publish an event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                       DateTimeOffset? scheduled = null,
                                                       CancellationToken cancellationToken = default)
            where TEvent : class
        {
            // Add diagnostics headers
            @event.Headers.AddIfNotDefault(DiagnosticHeaders.ActivityId, Activity.Current?.Id);

            // Set properties that may be missing
            @event.EventId ??= Guid.NewGuid().ToString();
            @event.Sent ??= DateTimeOffset.UtcNow;

            // Publish on the transport
            var transport = GetTransportForEvent<TEvent>();
            return await transport.PublishAsync(@event: @event,
                                                scheduled: scheduled,
                                                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Publish a batch of events.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="events">The events to publish.</param>
        /// <param name="scheduled">
        /// The time at which the event should be availed for consumption.
        /// Set null for immediate availability.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                              DateTimeOffset? scheduled = null,
                                                              CancellationToken cancellationToken = default)
            where TEvent : class
        {
            foreach (var @event in events)
            {
                // Add diagnostics headers
                @event.Headers.AddIfNotDefault(DiagnosticHeaders.ActivityId, Activity.Current?.Id);

                // Set properties that may be missing
                @event.EventId ??= Guid.NewGuid().ToString();
                @event.Sent ??= DateTimeOffset.UtcNow;
            }

            // Publish on the transport
            var transport = GetTransportForEvent<TEvent>();
            return await transport.PublishAsync(events: events,
                                                scheduled: scheduled,
                                                cancellationToken: cancellationToken);
        }


        /// <summary>
        /// Cancel a scheduled event.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="id">The identifier of the scheduled event.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            // cancel on the transport
            var transport = GetTransportForEvent<TEvent>();
            await transport.CancelAsync<TEvent>(id: id, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Cancel a batch of scheduled events.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="ids">The identifiers of the scheduled events.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            // cancel on the transport
            var transport = GetTransportForEvent<TEvent>();
            await transport.CancelAsync<TEvent>(ids: ids, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // If a startup delay has been specified, apply it
            if (options.StartupDelay != null)
            {
                logger.LogInformation("Delaying bus startup for '{StartupDelay}'", options.StartupDelay);
                await Task.Delay(options.StartupDelay.Value, cancellationToken);
            }

            // Start the bus and its transports
            logger.StartingBus();
            foreach (var t in transports)
            {
                await t.StartAsync(cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop the bus and its transports
            logger.StoppingBus();
            foreach (var t in transports)
            {
                await t.StopAsync(cancellationToken);
            }
        }


        internal IEventBusTransport GetTransportForEvent<TEvent>()
        {
            // get the transport
            var reg = options.GetOrCreateEventRegistration<TEvent>();
            var transportType = options.RegisteredTransportNames[reg.TransportName];
            return transports.Single(t => t.GetType() == transportType);
        }
    }
}
