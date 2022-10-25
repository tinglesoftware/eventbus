using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Ids;
using Tingle.EventBus.Transports;

namespace Tingle.EventBus;

/// <summary>
/// The event bus
/// </summary>
public class EventBus
{
    private readonly IEventIdGenerator idGenerator;
    private readonly IList<IEventConfigurator> configurators;
    private readonly EventBusOptions options;
    private readonly ILogger logger;

    private readonly IReadOnlyDictionary<string, IEventBusTransport> transports;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="transportProvider"></param>
    /// <param name="idGenerator"></param>
    /// <param name="optionsAccessor"></param>
    /// <param name="configurators"></param>
    /// <param name="loggerFactory"></param>
    public EventBus(EventBusTransportProvider transportProvider,
                    IEventIdGenerator idGenerator,
                    IEnumerable<IEventConfigurator> configurators,
                    IOptions<EventBusOptions> optionsAccessor,
                    ILoggerFactory loggerFactory)
    {
        this.idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        this.configurators = configurators?.ToList() ?? throw new ArgumentNullException(nameof(configurators));
        options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        logger = loggerFactory?.CreateLogger(LogCategoryNames.EventBus) ?? throw new ArgumentNullException(nameof(loggerFactory));

        transports = transportProvider.GetTransports();
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
    public async Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                             DateTimeOffset? scheduled = null,
                                                             CancellationToken cancellationToken = default)
        where TEvent : class
    {
        if (scheduled != null && scheduled <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Scheduled time cannot be in the past.");
        }

        // Get the transport and add transport specific activity tags
        var (reg, transport) = GetTransportForEvent<TEvent>();

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Publish, ActivityKind.Producer);
        activity?.AddTag(ActivityTagNames.MessagingSystem, transport.Name);
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusEventsCount, 1);

        // Add diagnostics headers to event
        @event.Headers.AddIfNotDefault(HeaderNames.EventType, typeof(TEvent).FullName);
        @event.Headers.AddIfNotDefault(HeaderNames.ActivityId, activity?.Id);

        // Set properties that may be missing
        @event.Id ??= idGenerator.Generate(reg);
        @event.Sent ??= DateTimeOffset.UtcNow;

        // Add message specific activity tags
        activity?.AddTag(ActivityTagNames.MessagingMessageId, @event.Id);
        activity?.AddTag(ActivityTagNames.MessagingConversationId, @event.CorrelationId);

        // Publish on the transport
        return await transport.PublishAsync(@event: @event,
                                            registration: reg,
                                            scheduled: scheduled,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);
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
    public async Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                    DateTimeOffset? scheduled = null,
                                                                    CancellationToken cancellationToken = default)
        where TEvent : class
    {
        if (scheduled != null && scheduled <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Scheduled time cannot be in the past.");
        }

        // Get the transport and add transport specific activity tags
        var (reg, transport) = GetTransportForEvent<TEvent>();

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Publish, ActivityKind.Producer);
        activity?.AddTag(ActivityTagNames.MessagingSystem, transport.Name);
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusEventsCount, events.Count);

        foreach (var @event in events)
        {
            // Add diagnostics headers
            @event.Headers.AddIfNotDefault(HeaderNames.EventType, typeof(TEvent).FullName);
            @event.Headers.AddIfNotDefault(HeaderNames.ActivityId, Activity.Current?.Id);

            // Set properties that may be missing
            @event.Id ??= idGenerator.Generate(reg);
            @event.Sent ??= DateTimeOffset.UtcNow;
        }

        // Add message specific activity tags
        activity?.AddTag(ActivityTagNames.MessagingMessageId, string.Join(",", events.Select(e => e.Id)));
        activity?.AddTag(ActivityTagNames.MessagingConversationId, string.Join(",", events.Select(e => e.CorrelationId)));

        // Publish on the transport
        return await transport.PublishAsync(events: events,
                                            registration: reg,
                                            scheduled: scheduled,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Cancel a scheduled event.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="id">The scheduling identifier of the scheduled event.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Cancel, ActivityKind.Client);
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusEventsCount, 1);
        activity?.AddTag(ActivityTagNames.MessagingMessageId, id);

        // Get the transport and add transport specific activity tags
        var (reg, transport) = GetTransportForEvent<TEvent>();
        activity?.AddTag(ActivityTagNames.MessagingSystem, transport.Name);

        // Cancel on the transport
        await transport.CancelAsync<TEvent>(id: id, registration: reg, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel a batch of scheduled events.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="ids">The scheduling identifiers of the scheduled events.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Cancel, ActivityKind.Client);
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusEventsCount, ids.Count);
        activity?.AddTag(ActivityTagNames.MessagingMessageId, string.Join(",", ids));

        // Get the transport and add transport specific activity tags
        var (reg, transport) = GetTransportForEvent<TEvent>();
        activity?.AddTag(ActivityTagNames.MessagingSystem, transport.Name);

        // Cancel on the transport
        await transport.CancelAsync<TEvent>(ids: ids, registration: reg, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    ///
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Start the bus and its transports
        logger.StartingBus(transports.Count);
        foreach (var t in transports.Values)
        {
            await t.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    ///
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop the bus
        logger.StoppingBus();

        // Stop the transports in parallel
        logger.StoppingTransports();
        var tasks = transports.Values.Select(t => t.StopAsync(cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    internal (EventRegistration registration, IEventBusTransport transport) GetTransportForEvent<TEvent>()
    {
        // get the transport
        var reg = GetOrCreateRegistration<TEvent>();
        var transport = GetTransportForEvent(reg.TransportName!);

        // For events that were not configured (e.g. publish only events),
        // the IdFormat will still be null, we have to set it
        reg.IdFormat ??= transport.GetOptions()?.DefaultEventIdFormat ?? options.DefaultEventIdFormat;

        return (reg, transport);
    }

    internal IEventBusTransport GetTransportForEvent(string name) => transports[name];

    internal EventRegistration GetOrCreateRegistration<TEvent>()
    {
        // if there's already a registration for the event return it
        var eventType = typeof(TEvent);
        if (options.Registrations.TryGetValue(key: eventType, out var registration)) return registration;

        // at this point, the registration does not exist;
        // create it and add to the registrations for repeated use
        options.Registrations[eventType] = registration = new EventRegistration(eventType);

        // pass the registration via all the configurators.
        foreach (var cfg in configurators)
        {
            cfg.Configure(registration, options);
        }

        return registration;
    }
}
