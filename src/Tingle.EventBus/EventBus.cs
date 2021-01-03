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

        private readonly List<IEventBusTransport> transports;
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
            this.transports = transports?.ToList() ?? throw new ArgumentNullException(nameof(transports));
            options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
            logger = loggerFactory?.CreateLogger(CategoryNames.EventBus) ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks for health of the bus.
        /// This function can be used by the Health Checks framework and may throw and execption during execution.
        /// </summary>
        /// <param name="data">Additional key-value pairs describing the health of the bus.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A value indicating if the bus is healthly.</returns>
        public async Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                 CancellationToken cancellationToken = default)
        {
            var healthy = true;

            // Ensure each transport is healthy
            foreach (var t in transports)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get the name of the transport
                var name = Microsoft.Extensions.DependencyInjection.EventBusBuilder.GetTransportName(t.GetType());

                // Check the health on the transport
                var tdata = new Dictionary<string, object>();
                healthy &= await t.CheckHealthAsync(tdata, cancellationToken);

                // Combine the data dictionaries into one, keyed by the transport name
                data[name] = tdata;
            }

            return healthy;
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
            // Instrument publish call
            using var activity = EventBusActivitySource.ActivitySource.StartActivity(ActivityNames.Publish, ActivityKind.Producer);
            activity?.AddTag(ActivityTags.EventBusEventType, typeof(TEvent).FullName);

            // Add diagnostics headers to event
            @event.Headers.AddIfNotDefault(DiagnosticHeaders.ActivityId, activity?.Id);

            // Set properties that may be missing
            @event.Id ??= Guid.NewGuid().ToString();
            @event.Sent ??= DateTimeOffset.UtcNow;

            // Add message specific activity tags
            activity?.AddTag(ActivityTags.MessagingMessageId, @event.Id);
            activity?.AddTag(ActivityTags.MessagingConversationId, @event.CorrelationId);

            // Get the transport and add transport specific activity tags
            var transport = GetTransportForEvent<TEvent>();
            activity?.AddTag(ActivityTags.MessagingSystem, transport.Name);

            // Publish on the transport
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
            // Instrument publish call
            using var activity = EventBusActivitySource.ActivitySource.StartActivity(ActivityNames.Publish, ActivityKind.Producer);
            activity?.AddTag(ActivityTags.EventBusEventType, typeof(TEvent).FullName);

            foreach (var @event in events)
            {
                // Add diagnostics headers
                @event.Headers.AddIfNotDefault(DiagnosticHeaders.ActivityId, Activity.Current?.Id);

                // Set properties that may be missing
                @event.Id ??= Guid.NewGuid().ToString();
                @event.Sent ??= DateTimeOffset.UtcNow;
            }

            // Add message specific activity tags
            activity?.AddTag(ActivityTags.MessagingMessageId, string.Join(",", events.Select(e => e.Id)));
            activity?.AddTag(ActivityTags.MessagingConversationId, string.Join(",", events.Select(e => e.CorrelationId)));

            // Get the transport and add transport specific activity tags
            var transport = GetTransportForEvent<TEvent>();
            activity?.AddTag(ActivityTags.MessagingSystem, transport.Name);

            // Publish on the transport
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
                // We cannot await the call because it will cause other components not to start.
                // Instead, create a cancellation token linked to the one provided so that we can
                // stop startup if told to do so.
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _ = DelayThenStartTransportsAsync(options.StartupDelay.Value, cts.Token);
            }
            else
            {
                // Without a delay, just start the transports directly
                await StartTransports(cancellationToken);
            }
        }

        private async Task DelayThenStartTransportsAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            // With delayed startup, the error may dissappear since the call to this method is not awaited.
            // The appropriate logging needs to be done.
            try
            {
                logger.LogInformation("Delaying bus startup for '{Delay}'", delay);
                await Task.Delay(delay, cancellationToken);
                await StartTransports(cancellationToken);
            }
            catch (Exception ex)
                when (!(ex is OperationCanceledException || ex is TaskCanceledException)) // skip operation cancel
            {
                logger.LogError(ex, "Starting bus delayed error.");
            }
        }

        private async Task StartTransports(CancellationToken cancellationToken)
        {
            // Start the bus and its transports
            logger.LogDebug("Starting bus with {TransportsCount} transports.", transports.Count());
            foreach (var t in transports)
            {
                await t.StartAsync(cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop the bus and its transports in parallel
            logger.LogDebug("Stopping bus.");
            var tasks = transports.Select(t => t.StopAsync(cancellationToken));
            await Task.WhenAll(tasks);
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
