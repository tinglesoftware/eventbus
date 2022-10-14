using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Transports;

/// <summary>
/// Abstract implementation for an event bus transport.
/// </summary>
/// <typeparam name="TTransportOptions">The type used for configuring options of the transport</typeparam>
public abstract class EventBusTransportBase<TTransportOptions> : IEventBusTransportWithOptions, IEventBusTransport where TTransportOptions : EventBusTransportOptionsBase, new()
{
    private static readonly Regex CategoryNamePattern = new(@"Transport$", RegexOptions.Compiled);
    private readonly IServiceScopeFactory scopeFactory;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="transportOptionsAccessor"></param>
    /// <param name="loggerFactory"></param>
    public EventBusTransportBase(IServiceScopeFactory scopeFactory,
                                 IOptions<EventBusOptions> busOptionsAccessor,
                                 IOptions<TTransportOptions> transportOptionsAccessor,
                                 ILoggerFactory loggerFactory)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        BusOptions = busOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(busOptionsAccessor));
        TransportOptions = transportOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(transportOptionsAccessor));

        // Create a well-scoped logger
        var categoryName = $"{LogCategoryNames.Transports}.{GetType().Name}";
        categoryName = CategoryNamePattern.Replace(categoryName, string.Empty); // remove trailing "Transport"
        Logger = loggerFactory?.CreateLogger(categoryName) ?? throw new ArgumentNullException(nameof(loggerFactory));
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
    public string Name => TransportOptions.Name!;

    ///
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    EventBusTransportOptionsBase IEventBusTransportWithOptions.GetOptions() => TransportOptions;

    #region Publishing

    /// <inheritdoc/>
    public virtual async Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                     EventRegistration registration,
                                                                     DateTimeOffset? scheduled = null,
                                                                     CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // publish, with retry if specified
        var retryPolicy = registration.RetryPolicy;
        if (retryPolicy != null)
        {
            return await retryPolicy.ExecuteAsync(ct => PublishCoreAsync(@event, registration, scheduled, ct), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return await PublishCoreAsync(@event, registration, scheduled, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                            EventRegistration registration,
                                                                            DateTimeOffset? scheduled = null,
                                                                            CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // publish, with retry if specified
        var retryPolicy = registration.RetryPolicy;
        if (retryPolicy != null)
        {
            return await retryPolicy.ExecuteAsync(ct => PublishCoreAsync(events, registration, scheduled, ct), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return await PublishCoreAsync(events, registration, scheduled, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected abstract Task<ScheduledResult?> PublishCoreAsync<TEvent>(EventContext<TEvent> @event,
                                                                       EventRegistration registration,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <inheritdoc/>
    protected abstract Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                              EventRegistration registration,
                                                                              DateTimeOffset? scheduled = null,
                                                                              CancellationToken cancellationToken = default)
        where TEvent : class;

    #endregion

    #region Cancelling

    /// <inheritdoc/>
    public virtual async Task CancelAsync<TEvent>(string id, EventRegistration registration, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // cancel, with retry if specified
        var retryPolicy = registration.RetryPolicy;
        if (retryPolicy != null)
        {
            await retryPolicy.ExecuteAsync(ct => CancelCoreAsync<TEvent>(id, registration, ct), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CancelCoreAsync<TEvent>(id, registration, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public virtual async Task CancelAsync<TEvent>(IList<string> ids, EventRegistration registration, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // cancel, with retry if specified
        var retryPolicy = registration.RetryPolicy;
        if (retryPolicy != null)
        {
            await retryPolicy.ExecuteAsync(ct => CancelCoreAsync<TEvent>(ids, registration, ct), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CancelCoreAsync<TEvent>(ids, registration, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected abstract Task CancelCoreAsync<TEvent>(string id,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <inheritdoc/>
    protected abstract Task CancelCoreAsync<TEvent>(IList<string> ids,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
        where TEvent : class;

    #endregion

    /// <inheritdoc/>
    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        /*
         * Set the retry policy and unhandled error behaviour if not set.
         * Give priority to the transport default then the bus default.
        */
        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            // Set publish retry policy
            reg.RetryPolicy ??= TransportOptions.DefaultRetryPolicy;
            reg.RetryPolicy ??= BusOptions.DefaultRetryPolicy;

            foreach (var ecr in reg.Consumers)
            {
                // Set unhandled error behaviour
                ecr.UnhandledErrorBehaviour ??= TransportOptions.DefaultUnhandledConsumerErrorBehaviour;
                ecr.UnhandledErrorBehaviour ??= BusOptions.DefaultUnhandledConsumerErrorBehaviour;
            }
        }
        Logger.StartingTransport(registrations.Count, TransportOptions.EmptyResultsDelay);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.StoppingTransport();
        return Task.CompletedTask;
    }

    #region Serialization

    /// <summary>
    /// Deserialize an event from a stream of bytes.
    /// </summary>
    /// <typeparam name="TEvent">The event type to be deserialized.</typeparam>
    /// <param name="ctx">The <see cref="DeserializationContext"/> to use.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task<EventContext<TEvent>> DeserializeAsync<TEvent>(DeserializationContext ctx,
                                                                        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // Resolve the serializer
        var registration = ctx.Registration;
        var serializer = (IEventSerializer)ActivatorUtilities.GetServiceOrCreateInstance(ctx.ServiceProvider, registration.EventSerializerType!);

        // Deserialize the content into a context
        var context = await serializer.DeserializeAsync<TEvent>(ctx, cancellationToken).ConfigureAwait(false);

        // Ensure we are not null (throwing helps track the error)
        if (context is null)
        {
            throw new InvalidOperationException($"Deserialization from '{typeof(TEvent).Name}' resulted in null which is not allowed.");
        }

        return context;
    }

    /// <summary>
    /// Deserialize an event from a stream of bytes.
    /// </summary>
    /// <typeparam name="TEvent">The event type to be deserialized.</typeparam>
    /// <param name="scope">The scope in which to resolve required services.</param>
    /// <param name="body">The <see cref="BinaryData"/> containing the raw data.</param>
    /// <param name="contentType">The type of content contained in the <paramref name="body"/>.</param>
    /// <param name="registration">The bus registration for this event.</param>
    /// <param name="identifier">Identifier given by the transport for the event to be deserialized.</param>
    /// <param name="raw">The raw data provided by the transport without any manipulation.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task<EventContext<TEvent>> DeserializeAsync<TEvent>(IServiceScope scope,
                                                                        BinaryData body,
                                                                        ContentType? contentType,
                                                                        EventRegistration registration,
                                                                        string? identifier,
                                                                        object? raw,
                                                                        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var ctx = new DeserializationContext(scope.ServiceProvider, body, registration, identifier)
        {
            ContentType = contentType,
            RawTransportData = raw,
        };
        return await DeserializeAsync<TEvent>(ctx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serialize an event into a stream of bytes.
    /// </summary>
    /// <typeparam name="TEvent">The event type to be serialized.</typeparam>
    /// <param name="ctx">The <see cref="SerializationContext{T}"/> to use.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task SerializeAsync<TEvent>(SerializationContext<TEvent> ctx,
                                                CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // Resolve the serializer
        var registration = ctx.Registration;
        var serializer = (IEventSerializer)ActivatorUtilities.GetServiceOrCreateInstance(ctx.ServiceProvider, registration.EventSerializerType!);

        // Serialize
        await serializer.SerializeAsync(ctx, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serialize an event into a stream of bytes.
    /// </summary>
    /// <typeparam name="TEvent">The event type to be serialized.</typeparam>
    /// <param name="scope">The scope in which to resolve required services.</param>
    /// <param name="event">The context of the event to be serialized.</param>
    /// <param name="registration">The bus registration for this event.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task<BinaryData> SerializeAsync<TEvent>(IServiceScope scope,
                                                            EventContext<TEvent> @event,
                                                            EventRegistration registration,
                                                            CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var ctx = new SerializationContext<TEvent>(scope.ServiceProvider, @event, registration);
        await SerializeAsync(ctx, cancellationToken).ConfigureAwait(false);

        return ctx.Body!;
    }

    #endregion

    #region Consuming

    /// <summary>
    /// Push an incoming event to the consumer responsible for it.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <typeparam name="TConsumer">The type of consumer.</typeparam>
    /// <param name="registration">The <see cref="EventRegistration"/> for the current event.</param>
    /// <param name="ecr">The <see cref="EventConsumerRegistration"/> for the current event.</param>
    /// <param name="event">The context containing the event.</param>
    /// <param name="scope">The scope in which to resolve required services.</param>
    /// <returns>An <see cref="EventConsumeResult"/> representing the state of the action.</returns>
    /// <param name="cancellationToken"></param>
    protected async Task<EventConsumeResult> ConsumeAsync<TEvent, TConsumer>(EventRegistration registration,
                                                                             EventConsumerRegistration ecr,
                                                                             EventContext<TEvent> @event,
                                                                             IServiceScope scope,
                                                                             CancellationToken cancellationToken)
        where TConsumer : IEventConsumer<TEvent>
        where TEvent : class
    {
        try
        {
            // Resolve the consumer
            var consumer = ActivatorUtilities.GetServiceOrCreateInstance<TConsumer>(scope.ServiceProvider);

            // Invoke handler method, with retry if specified
            var retryPolicy = registration.RetryPolicy;
            if (retryPolicy != null)
            {
                await retryPolicy.ExecuteAsync(ct => consumer.ConsumeAsync(@event, ct), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await consumer.ConsumeAsync(@event, cancellationToken).ConfigureAwait(false);
            }

            return new EventConsumeResult(successful: true, exception: null);
        }
        catch (Exception ex)
        {
            Logger.ConsumeFailed(ecr.UnhandledErrorBehaviour, @event.Id, ex);
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
    protected IServiceScope CreateScope() => scopeFactory.CreateScope();

    #region Registrations

    /// <summary>
    /// Gets the consumer registrations for this transport.
    /// </summary>
    /// <returns></returns>
    protected ICollection<EventRegistration> GetRegistrations() => BusOptions.GetRegistrations(transportName: Name);

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
    protected IDisposable BeginLoggingScopeForConsume(string? id,
                                                      string? correlationId,
                                                      string? sequenceNumber = null,
                                                      IDictionary<string, string?>? extras = null)
    {
        var state = new Dictionary<string, string>();
        state.AddIfNotDefault(MetadataNames.Id, id);
        state.AddIfNotDefault(MetadataNames.CorrelationId, correlationId);
        state.AddIfNotDefault(MetadataNames.SequenceNumber, sequenceNumber);

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

    #endregion
}
