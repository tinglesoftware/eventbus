using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Azure.ServiceBus
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using Azure Service Bus.
    /// </summary>
    [TransportName(TransportNames.AzureServiceBus)]
    public class AzureServiceBusTransport : EventBusTransportBase<AzureServiceBusTransportOptions>
    {
        private readonly Dictionary<Type, ServiceBusSender> sendersCache = new();
        private readonly SemaphoreSlim sendersCacheLock = new(1, 1); // only one at a time.
        private readonly Dictionary<string, ServiceBusProcessor> processorsCache = new();
        private readonly SemaphoreSlim processorsCacheLock = new(1, 1); // only one at a time.
        private readonly SemaphoreSlim propertiesCacheLock = new(1, 1); // only one at a time.
        private readonly ServiceBusAdministrationClient managementClient;
        private readonly ServiceBusClient serviceBusClient;
        private NamespaceProperties? properties;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureServiceBusTransport(IServiceScopeFactory serviceScopeFactory,
                                        IOptions<EventBusOptions> busOptionsAccessor,
                                        IOptions<AzureServiceBusTransportOptions> transportOptionsAccessor,
                                        ILoggerFactory loggerFactory)
            : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            var cred = TransportOptions.Credentials!.Value!;
            var sbcOptions = new ServiceBusClientOptions { TransportType = TransportOptions.TransportType, };
            if (cred is AzureServiceBusTransportCredentials asbtc)
            {
                managementClient = new ServiceBusAdministrationClient(fullyQualifiedNamespace: asbtc.FullyQualifiedNamespace,
                                                                      credential: asbtc.TokenCredential);
                serviceBusClient = new ServiceBusClient(fullyQualifiedNamespace: asbtc.FullyQualifiedNamespace,
                                                        credential: asbtc.TokenCredential,
                                                        options: sbcOptions);
            }
            else
            {
                managementClient = new ServiceBusAdministrationClient(connectionString: (string)cred);
                serviceBusClient = new ServiceBusClient(connectionString: (string)cred, options: sbcOptions);
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                          CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Listing Queues ...");
            var queues = managementClient.GetQueuesRuntimePropertiesAsync(cancellationToken).AsPages();
            await foreach (var _ in queues) ; // there's nothing to do
            var registrations = GetRegistrations();
            if (registrations.Any(r => r.EntityKind == EntityKind.Broadcast))
            {
                Logger.LogDebug("Listing Topics ...");
                var topics = managementClient.GetTopicsRuntimePropertiesAsync(cancellationToken);
                await foreach (var t in topics)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Logger.LogDebug("Listing Subscriptions for '{TopicName}' topic ...", t.Name);
                    var subscriptions = managementClient.GetSubscriptionsRuntimePropertiesAsync(t.Name, cancellationToken);
                    await foreach (var _ in subscriptions) ; // there's nothing to do
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            // Get the namespace properties once at the start
            _ = await GetNamespacePropertiesAsync(cancellationToken);

            var registrations = GetRegistrations();
            foreach (var reg in registrations)
            {
                foreach (var ecr in reg.Consumers)
                {
                    var processor = await GetProcessorAsync(reg: reg, ecr: ecr, cancellationToken: cancellationToken);

                    // register handlers for error and processing
                    processor.ProcessErrorAsync += OnMessageFaultedAsync;
                    processor.ProcessMessageAsync += delegate (ProcessMessageEventArgs args)
                    {
                        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
                        var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);
                        return (Task)method.Invoke(this, new object[] { reg, ecr, processor, args, });
                    };

                    // start processing
                    Logger.LogInformation("Starting processing on {EntityPath}", processor.EntityPath);
                    await processor.StartProcessingAsync(cancellationToken: cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            var clients = processorsCache.Select(kvp => (key: kvp.Key, proc: kvp.Value)).ToList();
            foreach (var (key, proc) in clients)
            {
                Logger.LogDebug("Stopping client: {Processor}", key);

                try
                {
                    await proc.StopProcessingAsync(cancellationToken);
                    processorsCache.Remove(key);

                    Logger.LogDebug("Stopped processor for {Processor}", key);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(exception, "Stop processor faulted for {Processor}", key);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                          EventRegistration registration,
                                                                          DateTimeOffset? scheduled = null,
                                                                          CancellationToken cancellationToken = default)
        {
            using var scope = CreateScope();
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken);

            var message = new ServiceBusMessage(body)
            {
                MessageId = @event.Id,
                ContentType = @event.ContentType?.ToString(),
            };

            // If CorrelationId is present, set it
            if (@event.CorrelationId != null)
            {
                message.CorrelationId = @event.CorrelationId;
            }

            // If scheduled for later, set the value in the message
            if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
            {
                message.ScheduledEnqueueTime = scheduled.Value.UtcDateTime;
            }

            // If expiry is set in the future, set the ttl in the message
            if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
            {
                var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                message.TimeToLive = ttl;
            }

            // Add custom properties
            message.ApplicationProperties.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                                         .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                                         .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

            // Get the sender and send the message accordingly
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Sending {Id} to '{EntityPath}'. Scheduled: {Scheduled}",
                                  @event.Id,
                                  sender.EntityPath,
                                  scheduled);
            if (scheduled != null)
            {
                var seqNum = await sender.ScheduleMessageAsync(message: message,
                                                               scheduledEnqueueTime: message.ScheduledEnqueueTime,
                                                               cancellationToken: cancellationToken);
                return new ScheduledResult(id: seqNum, scheduled: scheduled.Value); // return the sequence number
            }
            else
            {
                await sender.SendMessageAsync(message, cancellationToken);
                return null; // no sequence number available
            }
        }

        /// <inheritdoc/>
        public override async Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                 EventRegistration registration,
                                                                                 DateTimeOffset? scheduled = null,
                                                                                 CancellationToken cancellationToken = default)
        {
            using var scope = CreateScope();
            var messages = new List<ServiceBusMessage>();
            foreach (var @event in events)
            {
                var body = await SerializeAsync(scope: scope,
                                                @event: @event,
                                                registration: registration,
                                                cancellationToken: cancellationToken);

                var message = new ServiceBusMessage(body)
                {
                    MessageId = @event.Id,
                    ContentType = @event.ContentType?.ToString(),
                };

                // If CorrelationId is present, set it
                if (@event.CorrelationId != null)
                {
                    message.CorrelationId = @event.CorrelationId;
                }

                // If scheduled for later, set the value in the message
                if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
                {
                    message.ScheduledEnqueueTime = scheduled.Value.UtcDateTime;
                }

                // If expiry is set in the future, set the ttl in the message
                if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
                {
                    var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                    message.TimeToLive = ttl;
                }

                // Add custom properties
                message.ApplicationProperties.AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                                             .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                                             .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

                messages.Add(message);
            }

            // Get the sender and send the messages accordingly
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Sending {EventsCount} messages to '{EntityPath}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                                  events.Count,
                                  sender.EntityPath,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.Id)));
            if (scheduled != null)
            {
                var seqNums = await sender.ScheduleMessagesAsync(messages: messages,
                                                                 scheduledEnqueueTime: messages.First().ScheduledEnqueueTime,
                                                                 cancellationToken: cancellationToken);
                return seqNums.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList(); // return the sequence numbers
            }
            else
            {
                await sender.SendMessagesAsync(messages: messages, cancellationToken: cancellationToken);
                return Array.Empty<ScheduledResult>(); // no sequence numbers available
            }
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(string id,
                                                       EventRegistration registration,
                                                       CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace", nameof(id));
            }

            if (!long.TryParse(id, out var seqNum))
            {
                throw new ArgumentException($"'{nameof(id)}' is malformed or invalid", nameof(id));
            }

            // get the sender and cancel the message accordingly
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Canceling scheduled message: {SequenceNumber} on {EntityPath}", seqNum, sender.EntityPath);
            await sender.CancelScheduledMessageAsync(sequenceNumber: seqNum, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(IList<string> ids,
                                                       EventRegistration registration,
                                                       CancellationToken cancellationToken = default)
        {
            if (ids is null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var seqNums = ids.Select(i =>
            {
                if (!long.TryParse(i, out var seqNum))
                {
                    throw new ArgumentException($"'{nameof(i)}' is malformed or invalid", nameof(i));
                }
                return seqNum;
            }).ToList();

            // get the sender and cancel the messages accordingly
            var sender = await GetSenderAsync(registration, cancellationToken);
            Logger.LogInformation("Canceling {EventsCount} scheduled messages on {EntityPath}:\r\n- {SequenceNumbers}",
                                  ids.Count,
                                  sender.EntityPath,
                                  string.Join("\r\n- ", seqNums));
            await sender.CancelScheduledMessagesAsync(sequenceNumbers: seqNums, cancellationToken: cancellationToken);
        }

        private async Task<ServiceBusSender> GetSenderAsync(EventRegistration reg, CancellationToken cancellationToken)
        {
            await sendersCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!sendersCache.TryGetValue(reg.EventType, out var sender))
                {
                    var name = reg.EventName!;

                    // Create the entity.
                    if (await ShouldUseQueueAsync(reg, cancellationToken))
                    {
                        // Ensure Queue is created
                        Logger.LogDebug("Creating sender for queue '{QueueName}'", name);
                        await CreateQueueIfNotExistsAsync(reg: reg, name: name, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Ensure topic is created
                        Logger.LogDebug("Creating sender for topic '{TopicName}'", name);
                        await CreateTopicIfNotExistsAsync(reg: reg, name: name, cancellationToken: cancellationToken);
                    }

                    // Create the sender
                    sender = serviceBusClient.CreateSender(queueOrTopicName: name);
                    sendersCache[reg.EventType] = sender;
                }

                return sender;
            }
            finally
            {
                sendersCacheLock.Release();
            }
        }

        private async Task<ServiceBusProcessor> GetProcessorAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
        {
            await processorsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var topicName = reg.EventName!;
                var subscriptionName = ecr.ConsumerName!;

                var key = $"{topicName}/{subscriptionName}";
                if (!processorsCache.TryGetValue(key, out var processor))
                {
                    // Create the processor options
                    var sbpo = new ServiceBusProcessorOptions
                    {
                        // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                        // Set it according to how many messages the application wants to process in parallel.
                        MaxConcurrentCalls = 1,

                        // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                        // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                        AutoCompleteMessages = false,

                        // Set the period of time for which to keep renewing the lock token
                        MaxAutoLockRenewalDuration = TransportOptions.DefaultMaxAutoLockRenewDuration,

                        // Set the number of items to be cached locally
                        PrefetchCount = TransportOptions.DefaultPrefetchCount,
                    };

                    // Allow for the defaults to be overridden
                    TransportOptions.SetupProcessorOptions?.Invoke(reg, ecr, sbpo);

                    // Create the processor.
                    if (await ShouldUseQueueAsync(reg, cancellationToken))
                    {
                        // Ensure Queue is created
                        await CreateQueueIfNotExistsAsync(reg: reg, name: topicName, cancellationToken: cancellationToken);

                        // Create the processor for the Queue
                        Logger.LogDebug("Creating processor for queue '{QueueName}'", topicName);
                        processor = serviceBusClient.CreateProcessor(queueName: topicName, options: sbpo);
                    }
                    else
                    {
                        // Ensure Topic is created before creating the Subscription
                        await CreateTopicIfNotExistsAsync(reg: reg, name: topicName, cancellationToken: cancellationToken);

                        // Ensure Subscription is created
                        await CreateSubscriptionIfNotExistsAsync(ecr: ecr,
                                                                 topicName: topicName,
                                                                 subscriptionName: subscriptionName,
                                                                 cancellationToken: cancellationToken);

                        // Create the processor for the Subscription
                        Logger.LogDebug("Creating processor for topic '{TopicName}' and subscription '{Subscription}'",
                                        topicName,
                                        subscriptionName);
                        processor = serviceBusClient.CreateProcessor(topicName: topicName,
                                                                     subscriptionName: subscriptionName,
                                                                     options: sbpo);
                    }

                    processorsCache[key] = processor;
                }

                return processor;
            }
            finally
            {
                processorsCacheLock.Release();
            }
        }

        private async Task CreateQueueIfNotExistsAsync(EventRegistration reg, string name, CancellationToken cancellationToken)
        {
            // if entity creation is not enabled, just return
            if (!TransportOptions.EnableEntityCreation)
            {
                Logger.LogTrace("Entity creation is disabled. Queue creation skipped");
                return;
            }

            // If the queue does not exist, create it
            Logger.LogDebug("Checking if queue '{QueueName}' exists", name);
            if (!await managementClient.QueueExistsAsync(name: name, cancellationToken: cancellationToken))
            {
                Logger.LogTrace("Queue '{QueueName}' does not exist, preparing creation.", name);
                var options = new CreateQueueOptions(name: name)
                {
                    // set the defaults for a queue here
                    Status = EntityStatus.Active,
                    EnablePartitioning = false,
                    EnableBatchedOperations = true,
                    DeadLetteringOnMessageExpiration = true,
                    LockDuration = TransportOptions.DefaultLockDuration,
                    MaxDeliveryCount = TransportOptions.DefaultMaxDeliveryCount,
                };

                // Certain properties are not allowed in Basic Tier or have lower limits
                if (!await IsBasicTierAsync(cancellationToken))
                {
                    options.DefaultMessageTimeToLive = TransportOptions.DefaultMessageTimeToLive; // defaults to 14days in basic tier
                    options.RequiresDuplicateDetection = BusOptions.EnableDeduplication;
                    options.DuplicateDetectionHistoryTimeWindow = BusOptions.DuplicateDetectionDuration;
                    options.AutoDeleteOnIdle = TransportOptions.DefaultAutoDeleteOnIdle;
                }

                // Allow for the defaults to be overridden
                TransportOptions.SetupQueueOptions?.Invoke(reg, options);
                Logger.LogInformation("Creating queue '{QueueName}'", name);
                _ = await managementClient.CreateQueueAsync(options: options, cancellationToken: cancellationToken);
            }
        }

        private async Task CreateTopicIfNotExistsAsync(EventRegistration reg, string name, CancellationToken cancellationToken)
        {
            // if entity creation is not enabled, just return
            if (!TransportOptions.EnableEntityCreation)
            {
                Logger.LogTrace("Entity creation is disabled. Topic creation skipped");
                return;
            }

            // If the topic does not exist, create it
            Logger.LogDebug("Checking if topic '{TopicName}' exists", name);
            if (!await managementClient.TopicExistsAsync(name: name, cancellationToken: cancellationToken))
            {
                Logger.LogTrace("Topic '{TopicName}' does not exist, preparing creation.", name);
                var options = new CreateTopicOptions(name: name)
                {
                    Status = EntityStatus.Active,
                    EnablePartitioning = false,
                    RequiresDuplicateDetection = BusOptions.EnableDeduplication,
                    DuplicateDetectionHistoryTimeWindow = BusOptions.DuplicateDetectionDuration,
                    AutoDeleteOnIdle = TransportOptions.DefaultAutoDeleteOnIdle,
                    DefaultMessageTimeToLive = TransportOptions.DefaultMessageTimeToLive,
                    EnableBatchedOperations = true,
                };

                // Allow for the defaults to be overridden
                TransportOptions.SetupTopicOptions?.Invoke(reg, options);
                Logger.LogInformation("Creating topic '{TopicName}'", name);
                _ = await managementClient.CreateTopicAsync(options: options, cancellationToken: cancellationToken);
            }
        }

        private async Task CreateSubscriptionIfNotExistsAsync(EventConsumerRegistration ecr, string topicName, string subscriptionName, CancellationToken cancellationToken)
        {
            // if entity creation is not enabled, just return
            if (!TransportOptions.EnableEntityCreation)
            {
                Logger.LogTrace("Entity creation is disabled. Subscription creation skipped");
                return;
            }

            // If the subscription does not exist, create it
            Logger.LogDebug("Checking if subscription '{SubscriptionName}' under topic '{TopicName}' exists",
                            subscriptionName,
                            topicName);
            if (!await managementClient.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken))
            {
                Logger.LogTrace("Subscription '{SubscriptionName}' under topic '{TopicName}' does not exist, preparing creation.",
                                subscriptionName,
                                topicName);
                var options = new CreateSubscriptionOptions(topicName: topicName, subscriptionName: subscriptionName)
                {
                    Status = EntityStatus.Active,
                    AutoDeleteOnIdle = TransportOptions.DefaultAutoDeleteOnIdle,
                    DefaultMessageTimeToLive = TransportOptions.DefaultMessageTimeToLive,
                    EnableBatchedOperations = true,
                    DeadLetteringOnMessageExpiration = true,
                    LockDuration = TransportOptions.DefaultLockDuration,
                    MaxDeliveryCount = TransportOptions.DefaultMaxDeliveryCount,
                };

                // Allow for the defaults to be overridden
                TransportOptions.SetupSubscriptionOptions?.Invoke(ecr, options);
                Logger.LogInformation("Creating subscription '{SubscriptionName}' under topic '{TopicName}'",
                                      subscriptionName,
                                      topicName);
                await managementClient.CreateSubscriptionAsync(options: options, cancellationToken: cancellationToken);
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                     EventConsumerRegistration ecr,
                                                                     ServiceBusProcessor processor,
                                                                     ProcessMessageEventArgs args)
            where TEvent : class
            where TConsumer : IEventConsumer<TEvent>
        {
            var entityPath = processor.EntityPath;
            var message = args.Message;
            var messageId = message.MessageId;
            var cancellationToken = args.CancellationToken;

            message.ApplicationProperties.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

            using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                              correlationId: message.CorrelationId,
                                                              sequenceNumber: message.SequenceNumber.ToString(),
                                                              extras: new Dictionary<string, string?>
                                                              {
                                                                  ["EntityPath"] = entityPath,
                                                                  ["EnqueuedSequenceNumber"] = message.EnqueuedSequenceNumber.ToString(),
                                                              });

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
            var destination = await ShouldUseQueueAsync(reg, cancellationToken) ? reg.EventName : ecr.ConsumerName;
            activity?.AddTag(ActivityTagNames.MessagingDestination, destination); // name of the queue/subscription
            activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue"); // the spec does not know subscription so we can only use queue for both

            Logger.LogDebug("Processing '{MessageId}' from '{EntityPath}'", messageId, entityPath);
            using var scope = CreateScope();
            var contentType = new ContentType(message.ContentType);
            var context = await DeserializeAsync<TEvent>(scope: scope,
                                                         body: message.Body,
                                                         contentType: contentType,
                                                         registration: reg,
                                                         identifier: message.SequenceNumber.ToString(),
                                                         cancellationToken: cancellationToken);

            Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}' from '{EntityPath}'",
                                  messageId,
                                  context.Id,
                                  entityPath);

            // set the extras
            context.SetServiceBusReceivedMessage(message);

            var (successful, ex) = await ConsumeAsync<TEvent, TConsumer>(ecr: ecr,
                                                                         @event: context,
                                                                         scope: scope,
                                                                         cancellationToken: cancellationToken);

            // Decide the action to execute then execute
            var action = DecideAction(successful, ecr.UnhandledErrorBehaviour, processor.AutoCompleteMessages);
            Logger.LogDebug("Post Consume action: {Action} for message: {MessageId} from '{EntityPath}' containing Event: {EventId}.",
                            action,
                            messageId,
                            entityPath,
                            context.Id);

            if (action == PostConsumeAction.Complete)
            {
                await args.CompleteMessageAsync(message: message, cancellationToken: cancellationToken);
            }
            else if (action == PostConsumeAction.Throw)
            {
                if (ex is not null)
                {
                    throw ex; // Any better way to do this?
                }
            }
            else if (action == PostConsumeAction.Deadletter)
            {
                await args.DeadLetterMessageAsync(message, cancellationToken: cancellationToken);
            }
            else if (action == PostConsumeAction.Abandon)
            {
                await args.AbandonMessageAsync(message, cancellationToken: cancellationToken);
            }
        }

        private Task OnMessageFaultedAsync(ProcessErrorEventArgs args)
        {
            Logger.LogError(args.Exception,
                            "Message receiving faulted. Namespace:{FullyQualifiedNamespace}, Entity Path: {EntityPath}, Source: {ErrorSource}",
                            args.FullyQualifiedNamespace,
                            args.EntityPath,
                            args.ErrorSource);
            return Task.CompletedTask;
        }

        private async Task<bool> ShouldUseQueueAsync(EventRegistration reg, CancellationToken cancellationToken)
        {
            /*
             * Queues are used in the basic tier or when explicitly mapped to Queue.
             * Otherwise, Topics and Subscriptions are used.
            */
            return reg.EntityKind == EntityKind.Queue || await IsBasicTierAsync(cancellationToken);
        }

        private async Task<bool> IsBasicTierAsync(CancellationToken cancellationToken)
        {
            var np = await GetNamespacePropertiesAsync(cancellationToken);
            return np.MessagingSku == MessagingSku.Basic;
        }

        private async Task<NamespaceProperties> GetNamespacePropertiesAsync(CancellationToken cancellationToken)
        {
            await propertiesCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (properties is null)
                {
                    properties = await managementClient.GetNamespacePropertiesAsync(cancellationToken);
                }
            }
            finally
            {
                propertiesCacheLock.Release();
            }

            return properties;
        }

        internal static PostConsumeAction? DecideAction(bool successful, UnhandledConsumerErrorBehaviour? behaviour, bool autoComplete)
        {
            /*
             * Possible actions
             * |-------------------------------------------------------------|
             * |--Successful--|--Behaviour--|--Auto Complete--|----Action----|
             * |    false     |    null     |     false       |    Abandon   |
             * |    false     |    null     |     true        |   throw ex   |
             * |    false     |  Deadletter |     false       |  Deadletter  |
             * |    false     |  Deadletter |     true        |   throw ex   |
             * |    false     |   Discard   |     false       |   Complete   |
             * |    false     |   Discard   |     true        |    Nothing   |
             * 
             * |    true      |    null     |     false       |   Complete   |
             * |    true      |    null     |     true        |    Nothing   |
             * |    true      |  Deadletter |     false       |   Complete   |
             * |    true      |  Deadletter |     true        |    Nothing   |
             * |    true      |   Discard   |     false       |   Complete   |
             * |    true      |   Discard   |     true        |    Nothing   |
             * |-------------------------------------------------------------|
             * 
             * Conclusion:
             * - When Successful = true the action is either null or Complete, controlled by AutoComplete
             * - When Successful = false, the action will be throw if AutoComplete=true
             * */

            if (successful) return autoComplete ? null : (PostConsumeAction?)PostConsumeAction.Complete;

            // At this point it is not successful
            if (autoComplete)
                return behaviour == UnhandledConsumerErrorBehaviour.Discard
                        ? null
                        : (PostConsumeAction?)PostConsumeAction.Throw;

            return behaviour switch
            {
                UnhandledConsumerErrorBehaviour.Deadletter => PostConsumeAction.Deadletter,
                UnhandledConsumerErrorBehaviour.Discard => PostConsumeAction.Complete,
                _ => PostConsumeAction.Abandon,
            };
        }
    }

    internal enum PostConsumeAction
    {
        Throw,
        Complete,
        Abandon,
        Deadletter
    }
}
