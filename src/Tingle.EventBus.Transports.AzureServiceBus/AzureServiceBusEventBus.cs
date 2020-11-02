using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.AzureServiceBus
{
    public class AzureServiceBusEventBus : EventBusBase<AzureServiceBusOptions>
    {
        private readonly ManagementClient managementClient;
        private readonly ILogger logger;

        private readonly Dictionary<Type, TopicClient> topicClientsCache = new Dictionary<Type, TopicClient>();
        private readonly SemaphoreSlim topicClientsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<string, SubscriptionClient> subscriptionClientsCache = new Dictionary<string, SubscriptionClient>();
        private readonly SemaphoreSlim subscriptionClientsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.

        public AzureServiceBusEventBus(IHostEnvironment environment,
                                       IServiceScopeFactory serviceScopeFactory,
                                       ManagementClient managementClient,
                                       IOptions<EventBusOptions> optionsAccessor,
                                       IOptions<AzureServiceBusOptions> transportOptionsAccessor,
                                       ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, optionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            this.managementClient = managementClient ?? throw new ArgumentNullException(nameof(managementClient));
            logger = loggerFactory?.CreateLogger<AzureServiceBusEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            _ = await managementClient.GetQueuesRuntimeInfoAsync();
            var topics = await managementClient.GetTopicsRuntimeInfoAsync();
            foreach (var t in topics)
            {
                _ = await managementClient.GetSubscriptionsRuntimeInfoAsync(t.Path);
            }

            return true;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = Options.GetRegistrations();
            foreach (var reg in registrations)
            {
                var sc = await GetSubscriptionClientAsync(reg: reg, cancellationToken);
                var options = new MessageHandlerOptions(OnMessageFaultedAsync)
                {
                    // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                    // Set it according to how many messages the application wants to process in parallel.
                    MaxConcurrentCalls = 1,

                    // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                    // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                    AutoComplete = false,
                };
                sc.RegisterMessageHandler(handler: (message, ct) =>
                {
                    var method = GetType().GetMethod(nameof(OnMessageReceivedAsync)).MakeGenericMethod(reg.EventType, reg.ConsumerType);
                    return (Task)method.Invoke(this, new object[] { sc, message, ct, });
                }, messageHandlerOptions: options);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            var clients = subscriptionClientsCache.Select(kvp => (key: kvp.Key, sc: kvp.Value)).ToList();
            foreach (var (key, sc) in clients)
            {
                logger.LogDebug("Closing client: {Subscription}", key);

                try
                {
                    if (!sc.IsClosedOrClosing)
                    {
                        await sc.CloseAsync().ConfigureAwait(false);
                        subscriptionClientsCache.Remove(key);
                    }

                    logger.LogDebug("Closed subscription client for {Subscription}", key);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Close client faulted for {Subscription}", key);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(ms, @event, cancellationToken);

            var message = new Message
            {
                MessageId = @event.EventId,
                CorrelationId = @event.CorrelationId,
                ContentType = contentType.ToString(),
                Body = ms.ToArray(),
            };

            // if scheduled for later, set the value in the message
            if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
            {
                message.ScheduledEnqueueTimeUtc = scheduled.Value.DateTime;
            }

            // if expiry is set in the future, set the ttl in the message
            if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
            {
                var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                message.TimeToLive = ttl;
            }

            // get the topic client and send the message
            var topicClient = await GetTopicClientAsync(Options.GetRegistration<TEvent>(), cancellationToken);
            await topicClient.SendAsync(message);

            // send the message depending on whether scheduled or not
            return scheduled != null ? message.SystemProperties.SequenceNumber.ToString() : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        {
            var messages = new List<Message>();
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(ms, @event, cancellationToken);

                var message = new Message
                {
                    MessageId = @event.EventId,
                    CorrelationId = @event.CorrelationId,
                    ContentType = contentType.ToString(),
                    Body = ms.ToArray(),
                };

                // if scheduled for later, set the value in the message
                if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
                {
                    message.ScheduledEnqueueTimeUtc = scheduled.Value.DateTime;
                }

                // if expiry is set in the future, set the ttl in the message
                if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
                {
                    var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                    message.TimeToLive = ttl;
                }

                messages.Add(message);
            }

            // get the topic client and send the messages
            var topicClient = await GetTopicClientAsync(Options.GetRegistration<TEvent>(), cancellationToken);
            await topicClient.SendAsync(messages);

            var sequenceNumbers = messages.Select(m => m.SystemProperties.SequenceNumber.ToString());
            return scheduled != null ? sequenceNumbers.ToList() : (IList<string>)Array.Empty<string>();
        }

        private async Task<TopicClient> GetTopicClientAsync(EventConsumerRegistration reg, CancellationToken cancellationToken)
        {
            await topicClientsCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!topicClientsCache.TryGetValue(reg.EventType, out var topicClient))
                {
                    var name = reg.EventName;

                    // ensure topic is created
                    await CreateTopicIfNotExistsAsync(topicName: name, cancellationToken: cancellationToken);

                    // create the topic client
                    var cs = TransportOptions.ConnectionStringBuilder.ToString();
                    topicClient = new TopicClient(connectionString: cs, entityPath: name);
                    topicClientsCache[reg.EventType] = topicClient;
                };

                return topicClient;
            }
            finally
            {
                topicClientsCacheLock.Release();
            }
        }

        private async Task<SubscriptionClient> GetSubscriptionClientAsync(EventConsumerRegistration reg, CancellationToken cancellationToken)
        {
            await subscriptionClientsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var topicName = reg.EventName;
                var subscriptionName = reg.ConsumerName;

                var key = $"{topicName}/{subscriptionName}";
                if (!subscriptionClientsCache.TryGetValue(key, out var subscriptionClient))
                {

                    // if the subscription does not exist, create it
                    if (!await managementClient.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken))
                    {
                        // ensure topic is created before creating the subscription
                        await CreateTopicIfNotExistsAsync(topicName: topicName, cancellationToken: cancellationToken);

                        var desc = new SubscriptionDescription(topicName, subscriptionName);

                        // TODO: set the defaults for a subscription here

                        // allow for the defaults to be overriden
                        TransportOptions.SetupSubscriptionDescription?.Invoke(desc);
                        await managementClient.CreateSubscriptionAsync(desc, cancellationToken);
                    }

                    // create the subscription client
                    var cs = TransportOptions.ConnectionStringBuilder.ToString();
                    subscriptionClient = new SubscriptionClient(connectionString: cs, topicPath: topicName, subscriptionName: subscriptionName);
                    subscriptionClientsCache[key] = subscriptionClient;
                }

                return subscriptionClient;
            }
            finally
            {
                subscriptionClientsCacheLock.Release();
            }
        }

        private async Task CreateTopicIfNotExistsAsync(string topicName, CancellationToken cancellationToken)
        {
            // if the topic does not exist, create it
            if (!await managementClient.TopicExistsAsync(topicName, cancellationToken))
            {
                var desc = new TopicDescription(topicName);

                // TODO: set the defaults for a topic here

                // allow for the defaults to be overriden
                TransportOptions.SetupTopicDescription?.Invoke(desc);
                _ = await managementClient.CreateTopicAsync(topicDescription: desc, cancellationToken: cancellationToken);
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(SubscriptionClient subscriptionClient, Message message, CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            using var log_scope = logger.BeginScope(new Dictionary<string, string>
            {
                ["MesageId"] = message.MessageId,
                ["CorrelationId"] = message.CorrelationId,
                ["SequenceNumber"] = message.SystemProperties.SequenceNumber.ToString(),
                ["EnqueuedSequenceNumber"] = message.SystemProperties.EnqueuedSequenceNumber.ToString(),
            });

            try
            {
                using var ms = new MemoryStream(message.Body);
                var contentType = new ContentType(message.ContentType);
                var context = await DeserializeAsync<TEvent>(ms, contentType, cancellationToken);
                await PushToConsumerAsync<TEvent, TConsumer>(context, cancellationToken);

                // complete the message
                await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing failed. Moving to deadletter.");
                await subscriptionClient.DeadLetterAsync(message.SystemProperties.LockToken);
            }
        }

        private Task OnMessageFaultedAsync(ExceptionReceivedEventArgs args)
        {
            logger.LogError(args.Exception,
                            "Message receiving faulted. Endpoint:{Endpoint}, Entity Path: {EntityPath}, Executing Action: {ExecutingAction}",
                            args.ExceptionReceivedContext.Endpoint,
                            args.ExceptionReceivedContext.EntityPath,
                            args.ExceptionReceivedContext.Action);
            return Task.CompletedTask;
        }
    }
}
