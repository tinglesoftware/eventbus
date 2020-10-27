using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tingle.EventBus.Abstractions;
using System.Text;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Tingle.EventBus.Transports.AzureServiceBus
{
    public class AzureServiceBusEventBus : EventBusBase
    {
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly ManagementClient managementClient;
        private readonly IEventSerializer eventSerializer;
        private readonly AzureServiceBusOptions serviceBusOptions;
        private readonly ILogger logger;

        private readonly Dictionary<Type, TopicClient> topicClientsCache = new Dictionary<Type, TopicClient>();
        private readonly SemaphoreSlim topicClientsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<string, SubscriptionClient> subscriptionClientsCache = new Dictionary<string, SubscriptionClient>();
        private readonly SemaphoreSlim subscriptionClientsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.

        public AzureServiceBusEventBus(IHostEnvironment environment,
                                       IServiceScopeFactory serviceScopeFactory,
                                       ManagementClient managementClient,
                                       IEventSerializer eventSerializer,
                                       IOptions<EventBusOptions> optionsAccessor,
                                       IOptions<AzureServiceBusOptions> serviceBusOptionsAccessor,
                                       ILoggerFactory loggerFactory)
            : base(environment, optionsAccessor, loggerFactory)
        {
            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            this.managementClient = managementClient ?? throw new ArgumentNullException(nameof(managementClient));
            this.eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
            serviceBusOptions = serviceBusOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(serviceBusOptionsAccessor));
            logger = loggerFactory?.CreateLogger<AzureServiceBusEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

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
                var sc = await GetSubscriptionClientAsync(eventType: reg.EventType, consumerType: reg.ConsumerType, cancellationToken);
                var options = new MessageHandlerOptions(OnMessageFaultedAsync)
                {
                    // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                    // Set it according to how many messages the application wants to process in parallel.
                    MaxConcurrentCalls = 1,

                    // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                    // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                    AutoComplete = false,
                };
                sc.RegisterMessageHandler(handler: (message, ct) => OnMessageReceivedAsync(reg, sc, message, ct), messageHandlerOptions: options);
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

                    logger.LogDebug("Closed subscription client for: {Subscription}", key);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Close client faulted: {Subscription}", key);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            @event.Headers ??= new EventHeaders();
            @event.Headers.MessageId ??= Guid.NewGuid().ToString();

            using var ms = await eventSerializer.ToStreamAsync(@event, Encoding.UTF8, cancellationToken);

            var message = new Message
            {
                MessageId = @event.Headers.MessageId,
                CorrelationId = @event.Headers.CorrelationId,
                Body = ms.ToArray(),
            };

            // get the topic client
            var topicClient = await GetTopicClientAsync(typeof(TEvent), cancellationToken);

            // send the message depending on whether scheduled or not
            if (scheduled != null)
            {
                var seqNumber = await topicClient.ScheduleMessageAsync(message, scheduled.Value);
                return Convert.ToString(seqNumber);
            }
            else
            {
                await topicClient.SendAsync(message);
                return null;
            }
        }

        private async Task<TopicClient> GetTopicClientAsync(Type eventType, CancellationToken cancellationToken)
        {
            await topicClientsCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!topicClientsCache.TryGetValue(eventType, out var topicClient))
                {
                    var name = GetEventName(eventType);

                    // ensure topic is created
                    await CreateTopicIfNotExistsAsync(topicName: name, cancellationToken: cancellationToken);

                    // create the topic client
                    var cs = serviceBusOptions.ConnectionStringBuilder.ToString();
                    topicClient = new TopicClient(connectionString: cs, entityPath: name);
                    topicClientsCache[eventType] = topicClient;
                };

                return topicClient;
            }
            finally
            {
                topicClientsCacheLock.Release();
            }
        }

        private async Task<SubscriptionClient> GetSubscriptionClientAsync(Type eventType, Type consumerType, CancellationToken cancellationToken)
        {
            await subscriptionClientsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var topicName = GetEventName(eventType);
                var subscriptionName = GetConsumerName(consumerType);

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
                        serviceBusOptions.SetupSubscriptionDescription?.Invoke(desc);
                        await managementClient.CreateSubscriptionAsync(desc, cancellationToken);
                    }

                    // create the subscription client
                    var cs = serviceBusOptions.ConnectionStringBuilder.ToString();
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
                serviceBusOptions.SetupTopicDescription?.Invoke(desc);
                _ = await managementClient.CreateTopicAsync(topicDescription: desc, cancellationToken: cancellationToken);
            }
        }

        private async Task OnMessageReceivedAsync(EventConsumerRegistration registration, SubscriptionClient subscriptionClient, Message message, CancellationToken cancellationToken)
        {
            using var log_scope = logger.BeginScope(new Dictionary<string, string>
            {
                ["MesageId"] = message.MessageId,
                ["CorrelationId"] = message.CorrelationId,
                ["SequenceNumber"] = message.SystemProperties.SequenceNumber.ToString(),
                ["EnqueuedSequenceNumber"] = message.SystemProperties.EnqueuedSequenceNumber.ToString(),
            });

            // get the method to invoke
            var consumerType = registration.ConsumerType;
            var methodName = nameof(IEventBusConsumer<int>.ConsumeAsync);
            var mi = consumerType.GetMethod(methodName);
            mi = mi.MakeGenericMethod(registration.EventType);

            // resolve the conumer
            using var scope = serviceScopeFactory.CreateScope();
            var provider = scope.ServiceProvider;
            var consumer = provider.GetRequiredService(consumerType);

            try
            {
                var ms = new MemoryStream(message.Body);
                var eventContext = await eventSerializer.FromStreamAsync(ms, registration.EventType, Encoding.UTF8, cancellationToken);
                var tsk = (Task)mi.Invoke(consumer, new[] { eventContext, cancellationToken, });
                await tsk.ConfigureAwait(false);

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
