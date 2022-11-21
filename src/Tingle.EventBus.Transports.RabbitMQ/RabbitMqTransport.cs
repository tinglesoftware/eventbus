using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Diagnostics;
using System.Net.Mime;
using System.Net.Sockets;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports.RabbitMQ;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using RabbitMQ.
/// </summary>
public class RabbitMqTransport : EventBusTransport<RabbitMqTransportOptions>, IDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly EventBusConcurrentDictionary<string, IModel> subscriptionChannelsCache = new();
    private RetryPolicy? retryPolicy;

    private IConnection? connection;
    private bool disposed;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public RabbitMqTransport(IServiceScopeFactory serviceScopeFactory,
                             IOptions<EventBusOptions> busOptionsAccessor,
                             IOptionsMonitor<RabbitMqTransportOptions> optionsMonitor,
                             ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory) { }

    /// <inheritdoc/>
    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        await ConnectConsumersAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var channels = subscriptionChannelsCache.ToArray().Select(kvp => (key: kvp.Key, sc: kvp.Value)).ToList();
        foreach (var (key, t) in channels)
        {
            Logger.LogDebug("Closing channel: {Subscription}", key);

            try
            {
                var channel = await t.ConfigureAwait(false);
                if (!channel.IsClosed)
                {
                    channel.Close();
                    subscriptionChannelsCache.TryRemove(key, out _);
                }

                Logger.LogDebug("Closed channel for {Subscription}", key);
            }
            catch (Exception exception)
            {
                Logger.LogWarning(exception, "Close channel faulted for {Subscription}", key);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task<ScheduledResult?> PublishCoreAsync<TEvent>(EventContext<TEvent> @event,
                                                                             EventRegistration registration,
                                                                             DateTimeOffset? scheduled = null,
                                                                             CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            await TryConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // create channel, declare a fanout exchange
        using var channel = connection!.CreateModel();
        var name = registration.EventName;
        channel.ExchangeDeclare(exchange: name, type: "fanout");

        // serialize the event
        using var scope = CreateScope();
        var body = await SerializeAsync(scope: scope,
                                        @event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

        // publish message
        string? scheduledId = null;
        GetRetryPolicy().Execute(() =>
        {
            // setup properties
            var properties = channel.CreateBasicProperties();
            properties.MessageId = @event.Id;
            properties.CorrelationId = @event.CorrelationId;
            properties.ContentEncoding = @event.ContentType?.CharSet;
            properties.ContentType = @event.ContentType?.MediaType;

            // if scheduled for later, set the delay in the message
            if (scheduled != null)
            {
                var delay = Math.Max(0, (scheduled.Value - DateTimeOffset.UtcNow).TotalMilliseconds);
                if (delay > 0)
                {
                    properties.Headers["x-delay"] = (long)delay;
                    scheduledId = @event.Id!;
                }
            }

            // if expiry is set in the future, set the TTL in the message
            if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
            {
                var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                properties.Expiration = ((long)ttl.TotalMilliseconds).ToString();
            }

            // Add custom properties
            properties.Headers.ToEventBusWrapper()
                              .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                              .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                              .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

            // do actual publish
            Logger.LogInformation("Sending {Id} to '{ExchangeName}'. Scheduled: {Scheduled}",
                              @event.Id,
                              name,
                              scheduled);
            channel.BasicPublish(exchange: name,
                                 routingKey: "",
                                 basicProperties: properties,
                                 body: body);
        });

        return scheduledId != null && scheduled != null ? new ScheduledResult(id: scheduledId, scheduled: scheduled.Value) : null;
    }

    /// <inheritdoc/>
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                    EventRegistration registration,
                                                                                    DateTimeOffset? scheduled = null,
                                                                                    CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            await TryConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // create channel, declare a fanout exchange
        using var channel = connection!.CreateModel();
        var name = registration.EventName;
        channel.ExchangeDeclare(exchange: name, type: "fanout");

        using var scope = CreateScope();

        var serializedEvents = new List<(EventContext<TEvent>, ContentType?, BinaryData)>();
        foreach (var @event in events)
        {
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);
            serializedEvents.Add((@event, @event.ContentType, body));
        }

        GetRetryPolicy().Execute(() =>
        {
            var batch = channel.CreateBasicPublishBatch();
            foreach (var (@event, contentType, body) in serializedEvents)
            {
                // setup properties
                var properties = channel.CreateBasicProperties();
                properties.MessageId = @event.Id;
                properties.CorrelationId = @event.CorrelationId;
                properties.ContentEncoding = contentType?.CharSet;
                properties.ContentType = contentType?.MediaType;

                // if scheduled for later, set the delay in the message
                if (scheduled != null)
                {
                    var delay = Math.Max(0, (scheduled.Value - DateTimeOffset.UtcNow).TotalMilliseconds);
                    if (delay > 0)
                    {
                        properties.Headers["x-delay"] = (long)delay;
                    }
                }

                // if expiry is set in the future, set the TTL in the message
                if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
                {
                    var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                    properties.Expiration = ((long)ttl.TotalMilliseconds).ToString();
                }

                // Add custom properties
                properties.Headers.ToEventBusWrapper()
                                  .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                                  .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                                  .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

                // add to batch
                batch.Add(exchange: name, routingKey: "", mandatory: false, properties: properties, body: body);
            }

            // do actual publish
            Logger.LogInformation("Sending {EventsCount} messages to '{ExchangeName}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                              events.Count,
                              name,
                              scheduled,
                              string.Join("\r\n- ", events.Select(e => e.Id)));
            batch.Publish();
        });

        var messageIds = events.Select(m => m.Id!);
        return scheduled != null ? messageIds.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList() : Array.Empty<ScheduledResult>();
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(string id,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("RabbitMQ does not support canceling published messages.");
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(IList<string> ids,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("RabbitMQ does not support canceling published messages.");
    }

    private RetryPolicy GetRetryPolicy()
    {
        return retryPolicy ??= Policy.Handle<BrokerUnreachableException>()
                                     .Or<SocketException>()
                                     .WaitAndRetry(retryCount: Options.RetryCount,
                                                   sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                                                   onRetry: (ex, time) =>
                                                   {
                                                       Logger.LogError(ex, "RabbitMQ Client could not connect after {Timeout:n1}s ", time.TotalSeconds);
                                                   });
    }

    private async Task ConnectConsumersAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            await TryConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            var exchangeName = reg.EventName!;
            foreach (var ecr in reg.Consumers)
            {
                var queueName = ecr.ConsumerName!;

                var channel = await GetSubscriptionChannelAsync(exchangeName: exchangeName, queueName: queueName, cancellationToken).ConfigureAwait(false);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += delegate (object sender, BasicDeliverEventArgs @event)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags) ?? throw new InvalidOperationException("Methods should be null");
                    var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);
                    return (Task)method.Invoke(this, new object[] { reg, ecr, channel, @event, CancellationToken.None, })!; // do not chain CancellationToken
                };
                channel.BasicConsume(queue: queueName, autoAck: false, consumer);
            }
        }
    }

    private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                 EventConsumerRegistration ecr,
                                                                 IModel channel,
                                                                 BasicDeliverEventArgs args,
                                                                 CancellationToken cancellationToken)
        where TEvent : class
        where TConsumer : IEventConsumer<TEvent>
    {
        var messageId = args.BasicProperties?.MessageId;
        using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                          correlationId: args.BasicProperties?.CorrelationId,
                                                          extras: new Dictionary<string, string?>
                                                          {
                                                              ["RoutingKey"] = args.RoutingKey,
                                                              ["DeliveryTag"] = args.DeliveryTag.ToString(),
                                                          });

        object? parentActivityId = null;
        args.BasicProperties?.Headers.TryGetValue(MetadataNames.ActivityId, out parentActivityId);

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
        activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
        activity?.AddTag(ActivityTagNames.MessagingDestination, ecr.ConsumerName);
        activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue"); // only queues are possible

        Logger.LogDebug("Processing '{MessageId}'", messageId);
        using var scope = CreateScope();
        var contentType = GetContentType(args.BasicProperties);
        var context = await DeserializeAsync<TEvent>(scope: scope,
                                                     body: new BinaryData(args.Body),
                                                     contentType: contentType,
                                                     registration: reg,
                                                     identifier: messageId,
                                                     raw: args,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}'",
                              messageId,
                              context.Id);
        var (successful, ex) = await ConsumeAsync<TEvent, TConsumer>(registration: reg,
                                                                     ecr: ecr,
                                                                     @event: context,
                                                                     scope: scope,
                                                                     cancellationToken: cancellationToken).ConfigureAwait(false);

        // Decide the action to execute then execute
        var action = DecideAction(successful, ecr.UnhandledErrorBehaviour);
        Logger.LogDebug("Post Consume action: {Action} for message: {MessageId} containing Event: '{EventBusId}'.",
                        action,
                        messageId,
                        context.Id);

        if (action == PostConsumeAction.Acknowledge)
        {
            // Acknowledge the message
            Logger.LogDebug("Completing message: {MessageId}, {DeliveryTag}.", messageId, args.DeliveryTag);
            channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);
        }
        else if (action == PostConsumeAction.Deadletter || action == PostConsumeAction.Reject)
        {
            /*
             * requeue=false is the action that results in moving to a dead-letter queue
             * requeue=true will allow consumption later or by another consumer instance
             */
            var requeue = action == PostConsumeAction.Reject;
            channel.BasicNack(deliveryTag: args.DeliveryTag, multiple: false, requeue: requeue);
        }
    }

    private async Task<IModel> GetSubscriptionChannelAsync(string exchangeName, string queueName, CancellationToken cancellationToken)
    {
        Task<IModel> creator(string key, CancellationToken ct)
        {
            var channel = connection!.CreateModel();
            channel.ExchangeDeclare(exchange: exchangeName, type: "fanout");
            channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: "");
            channel.CallbackException += delegate (object? sender, CallbackExceptionEventArgs e)
            {
                Logger.LogError(e.Exception, "Callback exception for {Subscription}", key);
                var _ = ConnectConsumersAsync(CancellationToken.None); // do not await or chain token
            };

            return Task.FromResult(channel);
        }

        var key = $"{exchangeName}/{queueName}";
        var c = await subscriptionChannelsCache.GetOrAddAsync(key, creator, cancellationToken).ConfigureAwait(false);
        if (c.IsClosed)
        {
            subscriptionChannelsCache.TryRemove(key, out _);
            c.Dispose();
        }

        return await subscriptionChannelsCache.GetOrAddAsync(key, creator, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryConnectAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("RabbitMQ Client is trying to connect.");
        await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // if already connected, do not proceed
            if (IsConnected)
            {
                Logger.LogDebug("RabbitMQ Client is already connected.");
                return true;
            }

            GetRetryPolicy().Execute(() =>
            {
                connection = Options.ConnectionFactory!.CreateConnection();
            });

            if (IsConnected)
            {
                connection!.ConnectionShutdown += OnConnectionShutdown;
                connection.CallbackException += OnCallbackException;
                connection.ConnectionBlocked += OnConnectionBlocked;

                Logger.LogDebug("RabbitMQ Client acquired a persistent connection to '{HostName}'.",
                                connection.Endpoint.HostName);

                return true;
            }
            else
            {
                Logger.LogCritical("RabbitMQ Client connections could not be created and opened.");
                return false;
            }
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private bool TryConnect() => TryConnectAsync(CancellationToken.None).GetAwaiter().GetResult();

    private bool IsConnected => connection is not null && connection.IsOpen && !disposed;

    private void OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        if (disposed) return;

        Logger.LogWarning("RabbitMQ connection was blocked for {Reason}. Trying to re-connect...", e.Reason);

        TryConnect();
    }

    private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        if (disposed) return;

        Logger.LogWarning(e.Exception, "A RabbitMQ connection throw exception. Trying to re-connect...");

        TryConnect();
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs reason)
    {
        if (disposed) return;

        Logger.LogWarning("RabbitMQ connection shutdown. Trying to re-connect...");

        TryConnect();
    }

    private static ContentType? GetContentType(IBasicProperties? properties)
    {
        var contentType = properties?.ContentType;
        var contentEncoding = properties?.ContentEncoding ?? "utf-8"; // assume a default

        if (string.IsNullOrWhiteSpace(contentType)) return null;
        return new ContentType(contentType) { CharSet = contentEncoding };
    }

    /// 
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                try
                {
                    connection?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Trouble disposing RabbitMQ connection");
                }
            }

            disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal static PostConsumeAction? DecideAction(bool successful, UnhandledConsumerErrorBehaviour? behaviour)
    {
        /*
         * Possible actions
         * |-------------------------------------------------------------|
         * |--Successful--|--Behaviour--|----Action----|
         * |    false     |    null     |    Reject    |
         * |    false     |  Deadletter |  Deadletter  |
         * |    false     |   Discard   |  Acknowledge |
         * 
         * |    true      |    null     |  Acknowledge |
         * |    true      |  Deadletter |  Acknowledge |
         * |    true      |   Discard   |  Acknowledge |
         * |-------------------------------------------------------------|
         * 
         * Conclusion:
         * - When Successful = true the action is always Acknowledge
         * - When Successful = false, the action will be throw if AutoComplete=true
         * */

        if (successful) return PostConsumeAction.Acknowledge;

        return behaviour switch
        {
            UnhandledConsumerErrorBehaviour.Deadletter => PostConsumeAction.Deadletter,
            UnhandledConsumerErrorBehaviour.Discard => PostConsumeAction.Acknowledge,
            _ => PostConsumeAction.Reject,
        };
    }
}

internal enum PostConsumeAction
{
    Acknowledge,
    Reject,
    Deadletter
}
