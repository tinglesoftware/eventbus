using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.RabbitMQ
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using RabbitMQ.
    /// </summary>
    [TransportName(TransportNames.RabbitMq)]
    public class RabbitMqTransport : EventBusTransportBase<RabbitMqTransportOptions>, IDisposable
    {
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, IModel> subscriptionChannelsCache = new Dictionary<string, IModel>();
        private readonly SemaphoreSlim subscriptionChannelsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly RetryPolicy retryPolicy;

        private IConnection connection;
        private bool disposed;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public RabbitMqTransport(IServiceScopeFactory serviceScopeFactory,
                                 IOptions<EventBusOptions> busOptionsAccessor,
                                 IOptions<RabbitMqTransportOptions> transportOptionsAccessor,
                                 ILoggerFactory loggerFactory)
            : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            retryPolicy = Policy.Handle<BrokerUnreachableException>()
                                .Or<SocketException>()
                                .WaitAndRetry(retryCount: TransportOptions.RetryCount,
                                              sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                                              onRetry: (ex, time) =>
                                              {
                                                  Logger.LogError(ex, "RabbitMQ Client could not connect after {Timeout:n1}s ", time.TotalSeconds);
                                              });
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                          CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                await TryConnectAsync(cancellationToken);
            }

            using var channel = connection.CreateModel();
            return channel.IsOpen;
        }

        /// <inheritdoc/>
        public override async Task StartAsync(CancellationToken cancellationToken) => await ConnectConsumersAsync(cancellationToken);

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.StoppingTransport();
            var channels = subscriptionChannelsCache.Select(kvp => (key: kvp.Key, sc: kvp.Value)).ToList();
            foreach (var (key, channel) in channels)
            {
                Logger.LogDebug("Closing channel: {Subscription}", key);

                try
                {
                    if (!channel.IsClosed)
                    {
                        channel.Close();
                        subscriptionChannelsCache.Remove(key);
                    }

                    Logger.LogDebug("Closed channel for {Subscription}", key);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(exception, "Close channel faulted for {Subscription}", key);
                }
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                EventRegistration registration,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                await TryConnectAsync(cancellationToken);
            }

            // create channel, declare a fanout exchange
            using var channel = connection.CreateModel();
            var name = registration.EventName;
            channel.ExchangeDeclare(exchange: name, type: "fanout");

            // serialize the event
            using var scope = CreateScope();
            using var ms = new MemoryStream();
            await SerializeAsync(body: ms,
                                 @event: @event,
                                 registration: registration,
                                 scope: scope,
                                 cancellationToken: cancellationToken);

            // publish message
            string scheduledId = null;
            retryPolicy.Execute(() =>
            {
                // setup properties
                var properties = channel.CreateBasicProperties();
                properties.MessageId = @event.Id;
                properties.CorrelationId = @event.CorrelationId;
                properties.ContentEncoding = @event.ContentType.CharSet;
                properties.ContentType = @event.ContentType.MediaType;

                // if scheduled for later, set the delay in the message
                if (scheduled != null)
                {
                    var delay = Math.Max(0, (scheduled.Value - DateTimeOffset.UtcNow).TotalMilliseconds);
                    if (delay > 0)
                    {
                        properties.Headers["x-delay"] = (long)delay;
                        scheduledId = @event.Id;
                    }
                }

                // if expiry is set in the future, set the ttl in the message
                if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
                {
                    var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                    properties.Expiration = ((long)ttl.TotalMilliseconds).ToString();
                }

                // Add custom properties
                properties.Headers.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                                  .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                                  .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

                // do actual publish
                Logger.LogInformation("Sending {Id} to '{ExchangeName}'. Scheduled: {Scheduled}",
                                      @event.Id,
                                      name,
                                      scheduled);
                channel.BasicPublish(exchange: name,
                                     routingKey: "",
                                     basicProperties: properties,
                                     body: ms.ToArray());
            });

            return scheduledId;
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                       EventRegistration registration,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                await TryConnectAsync(cancellationToken);
            }

            // create channel, declare a fanout exchange
            using var channel = connection.CreateModel();
            var name = registration.EventName;
            channel.ExchangeDeclare(exchange: name, type: "fanout");

            using var scope = CreateScope();

            var serializedEvents = new List<(EventContext<TEvent>, ContentType, ReadOnlyMemory<byte>)>();
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                await SerializeAsync(body: ms,
                                     @event: @event,
                                     registration: registration,
                                     scope: scope,
                                     cancellationToken: cancellationToken);
                serializedEvents.Add((@event, @event.ContentType, ms.ToArray()));
            }

            retryPolicy.Execute(() =>
            {
                var batch = channel.CreateBasicPublishBatch();
                foreach (var (@event, contentType, body) in serializedEvents)
                {
                    // setup properties
                    var properties = channel.CreateBasicProperties();
                    properties.MessageId = @event.Id;
                    properties.CorrelationId = @event.CorrelationId;
                    properties.ContentEncoding = contentType.CharSet;
                    properties.ContentType = contentType.MediaType;

                    // if scheduled for later, set the delay in the message
                    if (scheduled != null)
                    {
                        var delay = Math.Max(0, (scheduled.Value - DateTimeOffset.UtcNow).TotalMilliseconds);
                        if (delay > 0)
                        {
                            properties.Headers["x-delay"] = (long)delay;
                        }
                    }

                    // if expiry is set in the future, set the ttl in the message
                    if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
                    {
                        var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                        properties.Expiration = ((long)ttl.TotalMilliseconds).ToString();
                    }

                    // Add custom properties
                    properties.Headers.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                                      .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                                      .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

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

            var messageIds = events.Select(m => m.Id);
            return scheduled != null ? messageIds.ToList() : (IList<string>)Array.Empty<string>();
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("RabbitMQ does not support canceling published messages.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("RabbitMQ does not support canceling published messages.");
        }

        private async Task ConnectConsumersAsync(CancellationToken cancellationToken)
        {
            if (!IsConnected)
            {
                await TryConnectAsync(cancellationToken);
            }

            var registrations = GetRegistrations();
            Logger.StartingTransport(registrations.Count, TransportOptions.EmptyResultsDelay);
            foreach (var ereg in registrations)
            {
                var exchangeName = ereg.EventName;
                foreach (var creg in ereg.Consumers)
                {
                    var queueName = creg.ConsumerName;

                    var channel = await GetSubscriptionChannelAsync(exchangeName: exchangeName, queueName: queueName, cancellationToken);
                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += delegate (object sender, BasicDeliverEventArgs @event)
                    {
                        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
                        var method = mt.MakeGenericMethod(ereg.EventType, creg.ConsumerType);
                        return (Task)method.Invoke(this, new object[] { ereg, creg, channel, @event, CancellationToken.None, }); // do not chain CancellationToken
                    };
                    channel.BasicConsume(queue: queueName, autoAck: false, consumer);
                }
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration ereg,
                                                                     EventConsumerRegistration creg,
                                                                     IModel channel,
                                                                     BasicDeliverEventArgs args,
                                                                     CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventConsumer<TEvent>
        {
            var messageId = args.BasicProperties?.MessageId;
            using var log_scope = Logger.BeginScopeForConsume(id: messageId,
                                                              correlationId: args.BasicProperties?.CorrelationId,
                                                              extras: new Dictionary<string, string>
                                                              {
                                                                  ["RoutingKey"] = args.RoutingKey,
                                                                  ["DeliveryTag"] = args.DeliveryTag.ToString(),
                                                              });

            args.BasicProperties.Headers.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
            activity?.AddTag(ActivityTagNames.MessagingDestination, creg.ConsumerName);
            activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue"); // only queues are possible

            try
            {
                Logger.LogDebug("Processing '{MessageId}'", messageId);
                using var scope = CreateScope();
                using var ms = new MemoryStream(args.Body.ToArray());
                var contentType = GetContentType(args.BasicProperties);
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: ereg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);
                Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}'",
                                      messageId,
                                      context.Id);
                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);

                // Acknowlege the message
                Logger.LogDebug("Completing message: {MessageId}, {DeliveryTag}.", messageId, args.DeliveryTag);
                channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event processing failed. Moving to deadletter.");
                channel.BasicNack(deliveryTag: args.DeliveryTag, multiple: false, requeue: false);
            }
        }

        private async Task<IModel> GetSubscriptionChannelAsync(string exchangeName, string queueName, CancellationToken cancellationToken)
        {
            await subscriptionChannelsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var key = $"{exchangeName}/{queueName}";
                if (!subscriptionChannelsCache.TryGetValue(key, out var channel)
                    || channel.IsClosed)
                {
                    // dispose existing channel
                    if (channel != null) channel.Dispose();

                    channel = connection.CreateModel();
                    channel.ExchangeDeclare(exchange: exchangeName, type: "fanout");
                    channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                    channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: "");
                    channel.CallbackException += delegate (object sender, CallbackExceptionEventArgs e)
                    {
                        Logger.LogError(e.Exception, "Callback exeception for {Subscription}", key);
                        var _ = ConnectConsumersAsync(CancellationToken.None); // do not await or chain token
                    };

                    subscriptionChannelsCache[key] = channel;
                }

                return channel;
            }
            finally
            {
                subscriptionChannelsCacheLock.Release();
            }
        }

        private async Task<bool> TryConnectAsync(CancellationToken cancellationToken)
        {
            Logger.LogDebug("RabbitMQ Client is trying to connect.");
            await connectionLock.WaitAsync(cancellationToken);

            try
            {
                // if already connected, do not proceed
                if (IsConnected)
                {
                    Logger.LogDebug("RabbitMQ Client is already connected.");
                    return true;
                }

                retryPolicy.Execute(() =>
                {
                    connection = TransportOptions.ConnectionFactory.CreateConnection();
                });

                if (IsConnected)
                {
                    connection.ConnectionShutdown += OnConnectionShutdown;
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

        private bool IsConnected => connection != null && connection.IsOpen && !disposed;

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            if (disposed) return;

            Logger.LogWarning("RabbitMQ connection was blocked for {Reason}. Trying to re-connect...", e.Reason);

            TryConnect();
        }

        private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            if (disposed) return;

            Logger.LogWarning(e.Exception, "A RabbitMQ connection throw exception. Trying to re-connect...");

            TryConnect();
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (disposed) return;

            Logger.LogWarning("RabbitMQ connection shutdown. Trying to re-connect...");

            TryConnect();
        }

        private static ContentType GetContentType(IBasicProperties properties)
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
                        connection.Dispose();
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
    }
}
