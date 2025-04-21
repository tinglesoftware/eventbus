using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Net.Sockets;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports.RabbitMQ;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using RabbitMQ.
/// </summary>
/// <param name="serviceScopeFactory"></param>
/// <param name="busOptionsAccessor"></param>
/// <param name="optionsMonitor"></param>
/// <param name="loggerFactory"></param>
public class RabbitMqTransport(IServiceScopeFactory serviceScopeFactory,
                               IOptions<EventBusOptions> busOptionsAccessor,
                               IOptionsMonitor<RabbitMqTransportOptions> optionsMonitor,
                               ILoggerFactory loggerFactory)
    : EventBusTransport<RabbitMqTransportOptions>(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory), IDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly EventBusConcurrentDictionary<string, IChannel> subscriptionChannelsCache = new();
    private ResiliencePipeline? resiliencePipeline;

    private IConnection? connection;
    private bool disposed;

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
                    await channel.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
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
    protected override async Task<ScheduledResult?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                                                EventRegistration registration,
                                                                                                                                DateTimeOffset? scheduled = null,
                                                                                                                                CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            await TryConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // create channel, declare a fanout exchange
        using var channel = await connection!.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var name = registration.EventName!;
        await channel.ExchangeDeclareAsync(exchange: name, type: "fanout", cancellationToken: cancellationToken).ConfigureAwait(false);

        // serialize the event
        var body = await SerializeAsync(@event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

        // publish message
        string? scheduledId = null;
        await GetResiliencePipeline().ExecuteAsync(async (ct) =>
        {
            // setup properties
            var properties = new BasicProperties
            {
                MessageId = @event.Id,
                CorrelationId = @event.CorrelationId,
                ContentEncoding = @event.ContentType?.CharSet,
                ContentType = @event.ContentType?.MediaType,
                Headers = new Dictionary<string, object?>(),
            };

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
            await channel.BasicPublishAsync(exchange: name,
                                            routingKey: "",
                                            mandatory: false,
                                            basicProperties: properties,
                                            body: body,
                                            cancellationToken: ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return scheduledId != null && scheduled != null ? new ScheduledResult(id: scheduledId, scheduled: scheduled.Value) : null;
    }

    /// <inheritdoc/>
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                                                       EventRegistration registration,
                                                                                                                                       DateTimeOffset? scheduled = null,
                                                                                                                                       CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            await TryConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // log warning when doing batch
        Logger.BatchingNotSupported();

        // create channel, declare a fanout exchange
        using var channel = await connection!.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var name = registration.EventName!;
        await channel.ExchangeDeclareAsync(exchange: name, type: "fanout", cancellationToken: cancellationToken).ConfigureAwait(false);

        var serializedEvents = new List<(EventContext<TEvent>, ContentType?, BinaryData)>();
        foreach (var @event in events)
        {
            var body = await SerializeAsync(@event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);
            serializedEvents.Add((@event, @event.ContentType, body));
        }

        await GetResiliencePipeline().ExecuteAsync(async (ct) =>
        {
            foreach (var (@event, contentType, body) in serializedEvents)
            {
                // setup properties
                var properties = new BasicProperties
                {
                    MessageId = @event.Id,
                    CorrelationId = @event.CorrelationId,
                    ContentEncoding = contentType?.CharSet,
                    ContentType = contentType?.MediaType,
                    Headers = new Dictionary<string, object?>(),
                };

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

                // do actual publish
                Logger.LogInformation("Sending {Id} to '{ExchangeName}'. Scheduled: {Scheduled}",
                                @event.Id,
                                name,
                                scheduled);
                await channel.BasicPublishAsync(exchange: name,
                                                routingKey: "",
                                                mandatory: false,
                                                basicProperties: properties,
                                                body: body,
                                                cancellationToken: ct).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);

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

    private ResiliencePipeline GetResiliencePipeline()
    {
        return resiliencePipeline ??= new ResiliencePipelineBuilder()
                                        .AddRetry(new RetryStrategyOptions
                                        {
                                            ShouldHandle = new PredicateBuilder().Handle<BrokerUnreachableException>()
                                                                                 .Handle<SocketException>(),
                                            DelayGenerator = args => new ValueTask<TimeSpan?>(TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber))),
                                            MaxRetryAttempts = Options.RetryCount,
                                            OnRetry = args =>
                                            {
                                                Logger.LogError(args.Outcome.Exception, "RabbitMQ Client could not connect after {Timeout:n1}s", args.RetryDelay.TotalSeconds);
                                                return new ValueTask();
                                            },
                                        })
                                        .Build();
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
                // queue names must be unique so add the exchange name so that we can tell to whom the queue belongs
                var queueName = BusOptions.Naming.Join(ecr.ConsumerName!, exchangeName);

                var channel = await GetSubscriptionChannelAsync(exchangeName: exchangeName, queueName: queueName, cancellationToken).ConfigureAwait(false);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (object sender, BasicDeliverEventArgs @event) => OnMessageReceivedAsync(reg, ecr, channel, @event, CancellationToken.None); // do not chain CancellationToken
                await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task OnMessageReceivedAsync(EventRegistration reg, EventConsumerRegistration ecr, IChannel channel, BasicDeliverEventArgs args, CancellationToken cancellationToken)
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
        args.BasicProperties?.Headers?.TryGetValue(MetadataNames.ActivityId, out parentActivityId);

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
        activity?.AddTag(ActivityTagNames.EventBusEventType, reg.EventType.FullName);
        activity?.AddTag(ActivityTagNames.EventBusConsumerType, ecr.ConsumerType.FullName);
        activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
        activity?.AddTag(ActivityTagNames.MessagingDestination, ecr.ConsumerName);
        activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue"); // only queues are possible

        Logger.LogDebug("Processing '{MessageId}'", messageId);
        using var scope = CreateServiceScope();
        var contentType = GetContentType(args.BasicProperties);
        var context = await DeserializeAsync(scope: scope,
                                             body: new BinaryData(args.Body),
                                             contentType: contentType,
                                             registration: reg,
                                             identifier: messageId,
                                             raw: args,
                                             deadletter: ecr.Deadletter,
                                             cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}'",
                              messageId,
                              context.Id);
        var (successful, ex) = await ConsumeAsync(scope, reg, ecr, context, cancellationToken).ConfigureAwait(false);
        if (ex != null)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);
        }

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
            await channel.BasicAckAsync(deliveryTag: args.DeliveryTag, multiple: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else if (action == PostConsumeAction.Deadletter || action == PostConsumeAction.Reject)
        {
            /*
             * requeue=false is the action that results in moving to a dead-letter queue
             * requeue=true will allow consumption later or by another consumer instance
             */
            var requeue = action == PostConsumeAction.Reject;
            await channel.BasicNackAsync(deliveryTag: args.DeliveryTag, multiple: false, requeue: requeue, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IChannel> GetSubscriptionChannelAsync(string exchangeName, string queueName, CancellationToken cancellationToken)
    {
        async Task<IChannel> creator(string key, CancellationToken ct)
        {
            var channel = await connection!.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);
            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: "fanout", cancellationToken: ct).ConfigureAwait(false);
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct).ConfigureAwait(false);
            await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: "", cancellationToken: ct).ConfigureAwait(false);
            channel.CallbackExceptionAsync += delegate (object sender, CallbackExceptionEventArgs e)
            {
                Logger.LogError(e.Exception, "Callback exception for {Subscription}", key);
                var _ = ConnectConsumersAsync(CancellationToken.None); // do not await or chain token
                return Task.CompletedTask;
            };

            return channel;
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

            await GetResiliencePipeline().ExecuteAsync(async (ct) =>
            {
                connection = await Options.ConnectionFactory!.CreateConnectionAsync(cancellationToken: ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            if (IsConnected)
            {
                connection!.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;

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

    private Task<bool> TryConnect() => TryConnectAsync(CancellationToken.None);

    private bool IsConnected => connection is not null && connection.IsOpen && !disposed;

    private async Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
    {
        if (disposed) return;

        Logger.LogWarning("RabbitMQ connection was blocked for {Reason}. Trying to re-connect...", e.Reason);

        await TryConnect().ConfigureAwait(false);
    }

    private async Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        if (disposed) return;

        Logger.LogWarning(e.Exception, "A RabbitMQ connection throw exception. Trying to re-connect...");

        await TryConnect().ConfigureAwait(false);
    }

    private async Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs reason)
    {
        if (disposed) return;

        Logger.LogWarning("RabbitMQ connection shutdown. Trying to re-connect...");

        await TryConnect().ConfigureAwait(false);
    }

    private static ContentType? GetContentType(IReadOnlyBasicProperties? properties)
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
