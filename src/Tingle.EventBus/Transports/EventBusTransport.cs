using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Transports;

/// <summary>
/// Abstract implementation for an event bus transport.
/// </summary>
/// <typeparam name="TOptions">The type used for configuring options of the transport</typeparam>
public abstract class EventBusTransport<TOptions> : IEventBusTransport where TOptions : EventBusTransportOptions, new()
{
    private static readonly Regex CategoryNamePattern = new(@"Transport$", RegexOptions.Compiled);
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptionsMonitor<TOptions> optionsMonitor;

    private readonly TaskCompletionSource<bool> startedTcs = new(false);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public EventBusTransport(IServiceScopeFactory scopeFactory,
                             IOptions<EventBusOptions> busOptionsAccessor,
                             IOptionsMonitor<TOptions> optionsMonitor,
                             ILoggerFactory loggerFactory)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        BusOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));

        // Create a well-scoped logger
        var categoryName = $"{LogCategoryNames.Transports}.{GetType().Name}";
        categoryName = CategoryNamePattern.Replace(categoryName, string.Empty); // remove trailing "Transport"
        Logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>Options for configuring the bus.</summary>
    protected EventBusOptions BusOptions { get; }

    /// <inheritdoc/>
    protected EventBusTransportRegistration Registration { get; private set; } = default!;

    /// <summary>Options for configuring the transport.</summary>
    protected TOptions Options { get; private set; } = default!;

    /// <summary>Logger for the transport.</summary>
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    public string Name => Registration.Name;

    /// <inheritdoc/>
    EventBusTransportOptions? IEventBusTransport.GetOptions() => Options;

    /// <inheritdoc/>
    public virtual void Initialize(EventBusTransportRegistration registration)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        Options = optionsMonitor.Get(registration.Name);
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        /*
         * Set the resilience pipeline and unhandled error behaviour if not set.
         * Give priority to the transport default then the bus default.
        */
        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            // Combine the resilience pipelines
            ResiliencePipelineHelper.CombineIfNeeded(BusOptions, Options, reg);

            foreach (var ecr in reg.Consumers)
            {
                // Set unhandled error behaviour
                ecr.UnhandledErrorBehaviour ??= Options.DefaultUnhandledConsumerErrorBehaviour;
                ecr.UnhandledErrorBehaviour ??= BusOptions.DefaultUnhandledConsumerErrorBehaviour;
            }
        }
        Logger.StartingTransport(Name, registrations.Count, Options.EmptyResultsDelay);

        await StartCoreAsync(cancellationToken).ConfigureAwait(false);

        startedTcs.TrySetResult(true); // signal completion of startup
    }

    /// <summary>Triggered when the bus host is ready to start.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns></returns>
    protected abstract Task StartCoreAsync(CancellationToken cancellationToken);

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.StoppingTransport(Name);
        await StopCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Triggered when the bus host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    /// <returns></returns>
    protected abstract Task StopCoreAsync(CancellationToken cancellationToken);

    private Task WaitStartedAsync(CancellationToken cancellationToken)
    {
        if (!Options.WaitTransportStarted!.Value) return Task.CompletedTask;

        var tsk = startedTcs.Task;
        if (tsk.Status is TaskStatus.RanToCompletion) return tsk;
        return Task.Run(() => tsk, cancellationToken); // allows for cancellation by caller
    }

    #region Publishing

    /// <inheritdoc/>
    public virtual async Task<ScheduledResult?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                                        EventRegistration registration,
                                                                                                                        DateTimeOffset? scheduled = null,
                                                                                                                        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // publish, with resilience pipelines
        await WaitStartedAsync(cancellationToken).ConfigureAwait(false);
        ResiliencePipelineHelper.CombineIfNeeded(BusOptions, Options, registration); // ensure pipeline is set for non-consumer events
        Logger.SendingEvent(eventBusId: @event.Id, transportName: Name, scheduled: scheduled);
        return await registration.ExecutionPipeline.ExecuteAsync(
            async ct => await PublishCoreAsync(@event, registration, scheduled, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task<IList<ScheduledResult>?> PublishAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                                               EventRegistration registration,
                                                                                                                               DateTimeOffset? scheduled = null,
                                                                                                                               CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // publish, with resilience pipelines
        await WaitStartedAsync(cancellationToken).ConfigureAwait(false);
        ResiliencePipelineHelper.CombineIfNeeded(BusOptions, Options, registration); // ensure pipeline is set for non-consumer events
        Logger.SendingEvents(events, Name, scheduled);
        return await registration.ExecutionPipeline.ExecuteAsync(
            async ct => await PublishCoreAsync(events, registration, scheduled, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Publish an event on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="registration">The registration for the event.</param>
    /// <param name="scheduled">
    /// The time at which the event should be availed for consumption.
    /// Set <see langword="null"/> for immediate availability.
    /// </param>
    /// <param name="cancellationToken"></param>
    protected abstract Task<ScheduledResult?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                                          EventRegistration registration,
                                                                                                                          DateTimeOffset? scheduled = null,
                                                                                                                          CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>Publish a batch of events on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="registration">The registration for the events.</param>
    /// <param name="scheduled">
    /// The time at which the event should be availed for consumption.
    /// Set <see langword="null"/> for immediate availability.
    /// </param>
    /// <param name="cancellationToken"></param>
    protected abstract Task<IList<ScheduledResult>?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                                                 EventRegistration registration,
                                                                                                                                 DateTimeOffset? scheduled = null,
                                                                                                                                 CancellationToken cancellationToken = default)
        where TEvent : class;

    #endregion

    #region Canceling

    /// <inheritdoc/>
    public virtual async Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(string id, EventRegistration registration, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // cancel, with resilience pipelines
        await WaitStartedAsync(cancellationToken).ConfigureAwait(false);
        ResiliencePipelineHelper.CombineIfNeeded(BusOptions, Options, registration); // ensure pipeline is set for non-consumer events
        Logger.CancelingEvent(id, Name);
        await registration.ExecutionPipeline.ExecuteAsync(
            async ct => await CancelCoreAsync<TEvent>(id, registration, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task CancelAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<string> ids, EventRegistration registration, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // cancel, with resilience pipelines
        await WaitStartedAsync(cancellationToken).ConfigureAwait(false);
        ResiliencePipelineHelper.CombineIfNeeded(BusOptions, Options, registration); // ensure pipeline is set for non-consumer events
        Logger.CancelingEvents(ids, Name);
        await registration.ExecutionPipeline.ExecuteAsync(
            async ct => await CancelCoreAsync<TEvent>(ids, registration, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Cancel a scheduled event on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="id">The scheduling identifier of the scheduled event.</param>
    /// <param name="registration">The registration for the event.</param>
    /// <param name="cancellationToken"></param>
    protected abstract Task CancelCoreAsync<TEvent>(string id,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>Cancel a batch of scheduled events on the transport.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="ids">The scheduling identifiers of the scheduled events.</param>
    /// <param name="registration">The registration for the events.</param>
    /// <param name="cancellationToken"></param>
    protected abstract Task CancelCoreAsync<TEvent>(IList<string> ids,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
        where TEvent : class;

    #endregion

    #region Serialization

    /// <summary>Deserialize an event from a stream of bytes.</summary>
    /// <param name="scope">The scope in which to resolve required services.</param>
    /// <param name="body">The <see cref="BinaryData"/> containing the raw data.</param>
    /// <param name="contentType">The type of content contained in the <paramref name="body"/>.</param>
    /// <param name="registration">The bus registration for this event.</param>
    /// <param name="identifier">Identifier given by the transport for the event to be deserialized.</param>
    /// <param name="raw">The raw data provided by the transport without any manipulation.</param>
    /// <param name="deadletter">Whether the event is from a dead-letter entity.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task<EventContext> DeserializeAsync(IServiceScope scope,
                                                        BinaryData body,
                                                        ContentType? contentType,
                                                        EventRegistration registration,
                                                        string? identifier,
                                                        object? raw,
                                                        bool deadletter,
                                                        CancellationToken cancellationToken = default)
    {
        // Resolve the serializer
        var provider = scope.ServiceProvider;
        var serializer = (IEventSerializer)ActivatorUtilities.GetServiceOrCreateInstance(provider, registration.EventSerializerType!);

        // Deserialize
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var ctx = new DeserializationContext(body, registration, deadletter, identifier)
        {
            ContentType = contentType,
            RawTransportData = raw,
        };
        return await registration.Deserializer(serializer, ctx, publisher, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serialize an event into a stream of bytes.</summary>
    /// <typeparam name="TEvent">The event type to be serialized.</typeparam>
    /// <param name="event">The context of the event to be serialized.</param>
    /// <param name="registration">The bus registration for this event.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task<BinaryData> SerializeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                               EventRegistration registration,
                                                                                                               CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // Resolve the serializer
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var serializer = (IEventSerializer)ActivatorUtilities.GetServiceOrCreateInstance(provider, registration.EventSerializerType!);

        // Serialize
        var ctx = new SerializationContext<TEvent>(@event, registration);
        await serializer.SerializeAsync(ctx, cancellationToken).ConfigureAwait(false);

        return ctx.Body!;
    }

    #endregion

    #region Consuming

    /// <summary>Push an incoming event to the consumer responsible for it.</summary>
    /// <param name="scope">The scope in which to resolve required services.</param>
    /// <param name="registration">The <see cref="EventRegistration"/> for the current event.</param>
    /// <param name="ecr">The <see cref="EventConsumerRegistration"/> for the current event.</param>
    /// <param name="event">The context containing the event.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>An <see cref="EventConsumeResult"/> representing the state of the action.</returns>
    protected async Task<EventConsumeResult> ConsumeAsync(IServiceScope scope,
                                                          EventRegistration registration,
                                                          EventConsumerRegistration ecr,
                                                          EventContext @event,
                                                          CancellationToken cancellationToken)
    {
        try
        {
            // Resolve the consumer
            var consumer = (IEventConsumer)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, ecr.ConsumerType);

            await ecr.Consume(consumer, registration, ecr, @event, cancellationToken).ConfigureAwait(false);

            return new EventConsumeResult(successful: true, exception: null);
        }
        catch (Exception ex)
        {
            Logger.ConsumeFailed(Name, ecr.UnhandledErrorBehaviour, @event.Id, ex);
            return new EventConsumeResult(successful: false, exception: ex);
        }
    }

    #endregion

    /// <summary>
    /// Create an <see cref="IServiceScope"/> which contains an <see cref="IServiceProvider"/>
    /// used to resolve dependencies from a newly created scope.
    /// </summary>
    /// <returns>
    /// An <see cref="IServiceScope"/> controlling the lifetime of the scope.
    /// Once this is disposed, any scoped services that have been resolved
    /// from the <see cref="IServiceScope.ServiceProvider"/> will also be disposed.
    /// </returns>
    protected IServiceScope CreateServiceScope() => scopeFactory.CreateScope();

    #region Registrations

    /// <summary>Gets the consumer registrations for this transport.</summary>
    protected ICollection<EventRegistration> GetRegistrations() => BusOptions.GetRegistrations(transportName: Name);

    #endregion

    #region Logging

    /// <summary>Begins a logical operation scope for logging.</summary>
    /// <param name="id"></param>
    /// <param name="correlationId"></param>
    /// <param name="sequenceNumber"></param>
    /// <param name="offset"></param>
    /// <param name="extras">The extras to put in the scope. (Optional)</param>
    /// <returns>A disposable object that ends the logical operation scope on dispose.</returns>
    protected IDisposable? BeginLoggingScopeForConsume(string? id,
                                                       string? correlationId,
                                                       string? sequenceNumber = null,
                                                       string? offset = null,
                                                       IDictionary<string, string?>? extras = null)
    {
        var state = new Dictionary<string, string>();
        
        var wrapped = state.ToEventBusWrapper()
                           .AddIfNotDefault(MetadataNames.Id, id)
                           .AddIfNotDefault(MetadataNames.CorrelationId, correlationId)
                           .AddIfNotDefault(MetadataNames.SequenceNumber, sequenceNumber)
                           .AddIfNotDefault(MetadataNames.Offset, offset);

        // if there are extras, add them
        if (extras != null)
        {
            foreach (var kvp in extras)
            {
                wrapped.AddIfNotDefault(kvp.Key, kvp.Value);
            }
        }

        // create the scope
        return Logger.BeginScope(state);
    }

    #endregion
}
