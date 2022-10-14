﻿using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Mime;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;

namespace Tingle.EventBus.Transports.Azure.ServiceBus;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using Azure Service Bus.
/// </summary>
public class AzureServiceBusTransport : EventBusTransport<AzureServiceBusTransportOptions>
{
    private readonly Dictionary<Type, ServiceBusSender> sendersCache = new();
    private readonly SemaphoreSlim sendersCacheLock = new(1, 1); // only one at a time.
    private readonly Dictionary<string, ServiceBusProcessor> processorsCache = new();
    private readonly SemaphoreSlim processorsCacheLock = new(1, 1); // only one at a time.
    private readonly SemaphoreSlim propertiesCacheLock = new(1, 1); // only one at a time.
    private readonly Lazy<ServiceBusAdministrationClient> managementClient;
    private readonly Lazy<ServiceBusClient> serviceBusClient;
    private NamespaceProperties? properties;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public AzureServiceBusTransport(IServiceScopeFactory serviceScopeFactory,
                                    IOptions<EventBusOptions> busOptionsAccessor,
                                    IOptionsMonitor<AzureServiceBusTransportOptions> optionsMonitor,
                                    ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory)
    {
        managementClient = new Lazy<ServiceBusAdministrationClient>(() =>
        {
            var cred = Options.Credentials.CurrentValue;
            if (cred is AzureServiceBusTransportCredentials asbtc)
            {
                return new ServiceBusAdministrationClient(fullyQualifiedNamespace: asbtc.FullyQualifiedNamespace,
                                                          credential: asbtc.TokenCredential);
            }
            else
            {
                return new ServiceBusAdministrationClient(connectionString: (string)cred);
            }
        });

        serviceBusClient = new Lazy<ServiceBusClient>(() =>
        {
            var cred = Options.Credentials.CurrentValue;
            var sbcOptions = new ServiceBusClientOptions { TransportType = Options.TransportType, };
            if (cred is AzureServiceBusTransportCredentials asbtc)
            {
                return new ServiceBusClient(fullyQualifiedNamespace: asbtc.FullyQualifiedNamespace,
                                            credential: asbtc.TokenCredential,
                                            options: sbcOptions);
            }
            else
            {
                return new ServiceBusClient(connectionString: (string)cred, options: sbcOptions);
            }
        });
    }

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);

        // Get the namespace properties once at the start
        _ = await GetNamespacePropertiesAsync(cancellationToken).ConfigureAwait(false);

        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            foreach (var ecr in reg.Consumers)
            {
                var processor = await GetProcessorAsync(reg: reg, ecr: ecr, cancellationToken: cancellationToken).ConfigureAwait(false);

                // register handlers for error and processing
                processor.ProcessErrorAsync += OnMessageFaultedAsync;
                processor.ProcessMessageAsync += delegate (ProcessMessageEventArgs args)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags) ?? throw new InvalidOperationException("Methods should be null");
                    var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);
                    return (Task)method.Invoke(this, new object[] { reg, ecr, processor, args, })!;
                };

                // start processing
                Logger.StartingProcessing(entityPath: processor.EntityPath);
                await processor.StartProcessingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        var clients = processorsCache.Select(kvp => (key: kvp.Key, proc: kvp.Value)).ToList();
        foreach (var (key, proc) in clients)
        {
            Logger.StoppingProcessor(processor: key);

            try
            {
                await proc.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
                processorsCache.Remove(key);

                Logger.StoppedProcessor(processor: key);
            }
            catch (Exception exception)
            {
                Logger.StopProcessorFaulted(processor: key, ex: exception);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task<ScheduledResult?> PublishCoreAsync<TEvent>(EventContext<TEvent> @event,
                                                                             EventRegistration registration,
                                                                             DateTimeOffset? scheduled = null,
                                                                             CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        var body = await SerializeAsync(scope: scope,
                                        @event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

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

        // If expiry is set in the future, set the TTL in the message
        if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
        {
            var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
            message.TimeToLive = ttl;
        }

        // Add custom properties
        message.ApplicationProperties.AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                                     .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                                     .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

        // Get the sender and send the message accordingly
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.SendingMessage(eventBusId: @event.Id, entityPath: sender.EntityPath, scheduled: scheduled);
        if (scheduled != null)
        {
            var seqNum = await sender.ScheduleMessageAsync(message: message,
                                                           scheduledEnqueueTime: message.ScheduledEnqueueTime,
                                                           cancellationToken: cancellationToken).ConfigureAwait(false);
            return new ScheduledResult(id: seqNum, scheduled: scheduled.Value); // return the sequence number
        }
        else
        {
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            return null; // no sequence number available
        }
    }

    /// <inheritdoc/>
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
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
                                            cancellationToken: cancellationToken).ConfigureAwait(false);

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

            // If expiry is set in the future, set the TTL in the message
            if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
            {
                var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                message.TimeToLive = ttl;
            }

            // Add custom properties
            message.ApplicationProperties.AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                                         .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                                         .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

            messages.Add(message);
        }

        // Get the sender and send the messages accordingly
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.SendingMessages(events: events, entityPath: sender.EntityPath, scheduled: scheduled);
        if (scheduled != null)
        {
            var seqNums = await sender.ScheduleMessagesAsync(messages: messages,
                                                             scheduledEnqueueTime: messages.First().ScheduledEnqueueTime,
                                                             cancellationToken: cancellationToken).ConfigureAwait(false);
            return seqNums.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList(); // return the sequence numbers
        }
        else
        {
            await sender.SendMessagesAsync(messages: messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Array.Empty<ScheduledResult>(); // no sequence numbers available
        }
    }

    /// <inheritdoc/>
    protected override async Task CancelCoreAsync<TEvent>(string id,
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
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.CancelingMessage(sequenceNumber: seqNum, entityPath: sender.EntityPath);
        await sender.CancelScheduledMessageAsync(sequenceNumber: seqNum, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task CancelCoreAsync<TEvent>(IList<string> ids,
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
        var sender = await GetSenderAsync(registration, cancellationToken).ConfigureAwait(false);
        Logger.CancelingMessages(sequenceNumbers: seqNums, entityPath: sender.EntityPath);
        await sender.CancelScheduledMessagesAsync(sequenceNumbers: seqNums, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<ServiceBusSender> GetSenderAsync(EventRegistration reg, CancellationToken cancellationToken)
    {
        await sendersCacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!sendersCache.TryGetValue(reg.EventType, out var sender))
            {
                var name = reg.EventName!;

                // Create the entity.
                if (await ShouldUseQueueAsync(reg, cancellationToken).ConfigureAwait(false))
                {
                    // Ensure Queue is created
                    Logger.CreatingQueueSender(queueName: name);
                    await CreateQueueIfNotExistsAsync(reg: reg, name: name, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Ensure topic is created
                    Logger.CreatingTopicSender(topicName: name);
                    await CreateTopicIfNotExistsAsync(reg: reg, name: name, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                // Create the sender
                sender = serviceBusClient.Value.CreateSender(queueOrTopicName: name);
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
        await processorsCacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);

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
                    MaxAutoLockRenewalDuration = Options.DefaultMaxAutoLockRenewDuration,

                    // Set the number of items to be cached locally
                    PrefetchCount = Options.DefaultPrefetchCount,
                };

                // Allow for the defaults to be overridden
                Options.SetupProcessorOptions?.Invoke(reg, ecr, sbpo);

                // Create the processor.
                if (await ShouldUseQueueAsync(reg, cancellationToken).ConfigureAwait(false))
                {
                    // Ensure Queue is created
                    await CreateQueueIfNotExistsAsync(reg: reg, name: topicName, cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Create the processor for the Queue
                    Logger.CreatingQueueProcessor(queueName: topicName);
                    processor = serviceBusClient.Value.CreateProcessor(queueName: topicName, options: sbpo);
                }
                else
                {
                    // Ensure Topic is created before creating the Subscription
                    await CreateTopicIfNotExistsAsync(reg: reg, name: topicName, cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Ensure Subscription is created
                    await CreateSubscriptionIfNotExistsAsync(ecr: ecr,
                                                             topicName: topicName,
                                                             subscriptionName: subscriptionName,
                                                             cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Create the processor for the Subscription
                    Logger.CreatingSubscriptionProcessor(topicName: topicName, subscriptionName: subscriptionName);
                    processor = serviceBusClient.Value.CreateProcessor(topicName: topicName,
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
        if (!Options.EnableEntityCreation)
        {
            Logger.QueueEntityCreationDisabled();
            return;
        }

        // If the queue does not exist, create it
        Logger.CheckingQueueExistence(queueName: name);
        if (!await managementClient.Value.QueueExistsAsync(name: name, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            Logger.CreatingQueuePreparation(queueName: name);
            var options = new CreateQueueOptions(name: name)
            {
                // set the defaults for a queue here
                Status = EntityStatus.Active,
                EnablePartitioning = false,
                EnableBatchedOperations = true,
                DeadLetteringOnMessageExpiration = true,
                LockDuration = Options.DefaultLockDuration,
                MaxDeliveryCount = Options.DefaultMaxDeliveryCount,
            };

            // Certain properties are not allowed in Basic Tier or have lower limits
            if (!await IsBasicTierAsync(cancellationToken).ConfigureAwait(false))
            {
                options.DefaultMessageTimeToLive = Options.DefaultMessageTimeToLive; // defaults to 14days in basic tier
                options.RequiresDuplicateDetection = BusOptions.EnableDeduplication;
                options.DuplicateDetectionHistoryTimeWindow = BusOptions.DuplicateDetectionDuration;
                options.AutoDeleteOnIdle = Options.DefaultAutoDeleteOnIdle;
            }

            // Allow for the defaults to be overridden
            Options.SetupQueueOptions?.Invoke(reg, options);
            Logger.CreatingQueue(queueName: name);
            _ = await managementClient.Value.CreateQueueAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CreateTopicIfNotExistsAsync(EventRegistration reg, string name, CancellationToken cancellationToken)
    {
        // if entity creation is not enabled, just return
        if (!Options.EnableEntityCreation)
        {
            Logger.TopicEntityCreationDisabled();
            return;
        }

        // If the topic does not exist, create it
        Logger.CheckingTopicExistence(topicName: name);
        if (!await managementClient.Value.TopicExistsAsync(name: name, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            Logger.CreatingTopicPreparation(topicName: name);
            var options = new CreateTopicOptions(name: name)
            {
                Status = EntityStatus.Active,
                EnablePartitioning = false,
                RequiresDuplicateDetection = BusOptions.EnableDeduplication,
                DuplicateDetectionHistoryTimeWindow = BusOptions.DuplicateDetectionDuration,
                AutoDeleteOnIdle = Options.DefaultAutoDeleteOnIdle,
                DefaultMessageTimeToLive = Options.DefaultMessageTimeToLive,
                EnableBatchedOperations = true,
            };

            // Allow for the defaults to be overridden
            Options.SetupTopicOptions?.Invoke(reg, options);
            Logger.CreatingTopic(topicName: name);
            _ = await managementClient.Value.CreateTopicAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CreateSubscriptionIfNotExistsAsync(EventConsumerRegistration ecr, string topicName, string subscriptionName, CancellationToken cancellationToken)
    {
        // if entity creation is not enabled, just return
        if (!Options.EnableEntityCreation)
        {
            Logger.SubscriptionEntityCreationDisabled();
            return;
        }

        // If the subscription does not exist, create it
        Logger.CheckingSubscriptionExistence(subscriptionName: subscriptionName, topicName: topicName);
        if (!await managementClient.Value.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false))
        {
            Logger.CreatingSubscriptionPreparation(subscriptionName: subscriptionName, topicName: topicName);
            var options = new CreateSubscriptionOptions(topicName: topicName, subscriptionName: subscriptionName)
            {
                Status = EntityStatus.Active,
                AutoDeleteOnIdle = Options.DefaultAutoDeleteOnIdle,
                DefaultMessageTimeToLive = Options.DefaultMessageTimeToLive,
                EnableBatchedOperations = true,
                DeadLetteringOnMessageExpiration = true,
                LockDuration = Options.DefaultLockDuration,
                MaxDeliveryCount = Options.DefaultMaxDeliveryCount,
            };

            // Allow for the defaults to be overridden
            Options.SetupSubscriptionOptions?.Invoke(ecr, options);
            Logger.CreatingSubscription(subscriptionName: subscriptionName, topicName: topicName);
            await managementClient.Value.CreateSubscriptionAsync(options: options, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                 EventConsumerRegistration ecr,
                                                                 ServiceBusProcessor processor,
                                                                 ProcessMessageEventArgs args)
        where TEvent : class
        where TConsumer : IEventConsumer<TEvent>
    {
        var entityPath = args.EntityPath;
        var message = args.Message;
        var messageId = message.MessageId;
        var cancellationToken = args.CancellationToken;

        message.ApplicationProperties.TryGetValue(MetadataNames.ActivityId, out var parentActivityId);

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
        var destination = await ShouldUseQueueAsync(reg, cancellationToken).ConfigureAwait(false) ? reg.EventName : ecr.ConsumerName;
        activity?.AddTag(ActivityTagNames.MessagingDestination, destination); // name of the queue/subscription
        activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue"); // the spec does not know subscription so we can only use queue for both

        Logger.ProcessingMessage(messageId: messageId, entityPath: entityPath);
        using var scope = CreateScope();
        var contentType = new ContentType(message.ContentType);
        var context = await DeserializeAsync<TEvent>(scope: scope,
                                                     body: message.Body,
                                                     contentType: contentType,
                                                     registration: reg,
                                                     identifier: message.SequenceNumber.ToString(),
                                                     raw: message,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);

        Logger.ReceivedMessage(sequenceNumber: message.SequenceNumber, eventBusId: context.Id, entityPath: entityPath);

        // set the extras
        context.SetServiceBusReceivedMessage(message);

        var (successful, ex) = await ConsumeAsync<TEvent, TConsumer>(registration: reg,
                                                                     ecr: ecr,
                                                                     @event: context,
                                                                     scope: scope,
                                                                     cancellationToken: cancellationToken).ConfigureAwait(false);

        // Decide the action to execute then execute
        var action = DecideAction(successful, ecr.UnhandledErrorBehaviour, processor.AutoCompleteMessages);
        Logger.PostConsumeAction(action: action, messageId: messageId, entityPath: entityPath, eventBusId: context.Id);

        if (action == PostConsumeAction.Complete)
        {
            await args.CompleteMessageAsync(message: message, cancellationToken: cancellationToken).ConfigureAwait(false);
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
            await args.DeadLetterMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else if (action == PostConsumeAction.Abandon)
        {
            await args.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private Task OnMessageFaultedAsync(ProcessErrorEventArgs args)
    {
        Logger.MessageReceivingFaulted(fullyQualifiedNamespace: args.FullyQualifiedNamespace,
                                       entityPath: args.EntityPath,
                                       errorSource: args.ErrorSource,
                                       ex: args.Exception);
        return Task.CompletedTask;
    }

    private async Task<bool> ShouldUseQueueAsync(EventRegistration reg, CancellationToken cancellationToken)
    {
        /*
         * Queues are used in the basic tier or when explicitly mapped to Queue.
         * Otherwise, Topics and Subscriptions are used.
        */
        return reg.EntityKind == EntityKind.Queue || await IsBasicTierAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsBasicTierAsync(CancellationToken cancellationToken)
    {
        var np = await GetNamespacePropertiesAsync(cancellationToken).ConfigureAwait(false);
        return np.MessagingSku == MessagingSku.Basic;
    }

    private async Task<NamespaceProperties> GetNamespacePropertiesAsync(CancellationToken cancellationToken)
    {
        await propertiesCacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            properties ??= await managementClient.Value.GetNamespacePropertiesAsync(cancellationToken).ConfigureAwait(false);
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
        if (autoComplete) return behaviour is UnhandledConsumerErrorBehaviour.Discard ? null : PostConsumeAction.Throw;

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
