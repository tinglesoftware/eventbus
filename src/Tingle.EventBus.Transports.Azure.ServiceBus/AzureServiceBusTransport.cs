using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports.Azure.ServiceBus;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using Azure Service Bus.
/// </summary>
public class AzureServiceBusTransport : EventBusTransport<AzureServiceBusTransportOptions>
{
    private readonly EventBusConcurrentDictionary<Type, ServiceBusSender> sendersCache = new();
    private readonly EventBusConcurrentDictionary<string, ServiceBusProcessor> processorsCache = new();
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
    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
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
                processor.ProcessMessageAsync += (ProcessMessageEventArgs args) => OnMessageReceivedAsync(reg, ecr, processor, args);

                // start processing
                Logger.StartingProcessing(entityPath: processor.EntityPath);
                await processor.StartProcessingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var clients = processorsCache.ToArray().Select(kvp => (key: kvp.Key, proc: kvp.Value)).ToList();
        foreach (var (key, t) in clients)
        {
            Logger.StoppingProcessor(processor: key);

            try
            {
                var proc = await t.ConfigureAwait(false);
                await proc.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
                processorsCache.TryRemove(key, out _);

                Logger.StoppedProcessor(processor: key);
            }
            catch (Exception exception)
            {
                Logger.StopProcessorFaulted(processor: key, ex: exception);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task<ScheduledResult?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                                                EventRegistration registration,
                                                                                                                                DateTimeOffset? scheduled = null,
                                                                                                                                CancellationToken cancellationToken = default)
    {
        var body = await SerializeAsync(@event: @event,
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
        message.ApplicationProperties.ToEventBusWrapper()
                                     .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
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
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                                                       EventRegistration registration,
                                                                                                                                       DateTimeOffset? scheduled = null,
                                                                                                                                       CancellationToken cancellationToken = default)
    {
        var messages = new List<ServiceBusMessage>();
        foreach (var @event in events)
        {
            var body = await SerializeAsync(@event: @event,
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
            message.ApplicationProperties.ToEventBusWrapper()
                                         .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
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

    private Task<ServiceBusSender> GetSenderAsync(EventRegistration reg, CancellationToken cancellationToken)
    {
        async Task<ServiceBusSender> creator(Type _, CancellationToken ct)
        {
            var name = reg.EventName!;

            // Create the entity.
            if (await ShouldUseQueueAsync(reg, ct).ConfigureAwait(false))
            {
                // Ensure Queue is created
                Logger.CreatingQueueSender(queueName: name);
                await CreateQueueIfNotExistsAsync(reg: reg, name: name, cancellationToken: ct).ConfigureAwait(false);
            }
            else
            {
                // Ensure topic is created
                Logger.CreatingTopicSender(topicName: name);
                await CreateTopicIfNotExistsAsync(reg: reg, name: name, cancellationToken: ct).ConfigureAwait(false);
            }

            // Create the sender
            return serviceBusClient.Value.CreateSender(queueOrTopicName: name);
        }
        return sendersCache.GetOrAddAsync(reg.EventType, creator, cancellationToken);
    }

    private Task<ServiceBusProcessor> GetProcessorAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
    {
        var topicName = reg.EventName!;
        var subscriptionName = ecr.ConsumerName!;
        var deadletter = ecr.Deadletter;

        async Task<ServiceBusProcessor> creator(string key, CancellationToken ct)
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

                // Set the sub-queue to be used
                SubQueue = deadletter ? SubQueue.DeadLetter : SubQueue.None,
            };

            // Allow for the defaults to be overridden
            Options.SetupProcessorOptions?.Invoke(reg, ecr, sbpo);

            // Create the processor.
            if (await ShouldUseQueueAsync(reg, ct).ConfigureAwait(false))
            {
                // Ensure Queue is created
                await CreateQueueIfNotExistsAsync(reg: reg, name: topicName, cancellationToken: ct).ConfigureAwait(false);

                // Create the processor for the Queue
                Logger.CreatingQueueProcessor(queueName: topicName);
                return serviceBusClient.Value.CreateProcessor(queueName: topicName, options: sbpo);
            }
            else
            {
                // Ensure Topic is created before creating the Subscription
                await CreateTopicIfNotExistsAsync(reg: reg, name: topicName, cancellationToken: ct).ConfigureAwait(false);

                // Ensure Subscription is created
                await CreateSubscriptionIfNotExistsAsync(ecr: ecr,
                                                         topicName: topicName,
                                                         subscriptionName: subscriptionName,
                                                         cancellationToken: ct).ConfigureAwait(false);

                // Create the processor for the Subscription
                Logger.CreatingSubscriptionProcessor(topicName: topicName, subscriptionName: subscriptionName);
                return serviceBusClient.Value.CreateProcessor(topicName: topicName, subscriptionName: subscriptionName, options: sbpo);
            }
        }

        var key = $"{topicName}/{subscriptionName}/{deadletter}";
        return processorsCache.GetOrAddAsync(key, creator, cancellationToken);
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
                options.AutoDeleteOnIdle = Options.DefaultAutoDeleteOnIdle;

                // set duplication detection
                var dedup_dur = reg.DuplicateDetectionDuration;
                if (dedup_dur is not null)
                {
                    options.RequiresDuplicateDetection = true;
                    options.DuplicateDetectionHistoryTimeWindow = SafeDuplicateDetectionHistoryTimeWindow(dedup_dur.Value);
                }
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
                AutoDeleteOnIdle = Options.DefaultAutoDeleteOnIdle,
                DefaultMessageTimeToLive = Options.DefaultMessageTimeToLive,
                EnableBatchedOperations = true,
            };

            // set duplication detection
            var dedup_dur = reg.DuplicateDetectionDuration;
            if (dedup_dur is not null)
            {
                options.RequiresDuplicateDetection = true;
                options.DuplicateDetectionHistoryTimeWindow = SafeDuplicateDetectionHistoryTimeWindow(dedup_dur.Value);
            }

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

    private static TimeSpan SafeDuplicateDetectionHistoryTimeWindow(TimeSpan value)
    {
        var ticks = value.Ticks;
        ticks = Math.Max(ticks, TimeSpan.FromSeconds(20).Ticks); // must be more than 20 seconds
        ticks = Math.Min(ticks, TimeSpan.FromDays(7).Ticks); // must be less than 7 days
        return TimeSpan.FromTicks(ticks);
    }

    private async Task OnMessageReceivedAsync(EventRegistration reg, EventConsumerRegistration ecr, ServiceBusProcessor processor, ProcessMessageEventArgs args)
    {
        var fqdn = args.FullyQualifiedNamespace;
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
                                                              [MetadataNames.FullyQualifiedNamespace] = fqdn,
                                                              [MetadataNames.EntityUri] = entityPath,
                                                              ["EnqueuedSequenceNumber"] = message.EnqueuedSequenceNumber.ToString(),
                                                          });

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
        activity?.AddTag(ActivityTagNames.EventBusEventType, reg.EventType.FullName);
        activity?.AddTag(ActivityTagNames.EventBusConsumerType, ecr.ConsumerType.FullName);
        activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
        var destination = await ShouldUseQueueAsync(reg, cancellationToken).ConfigureAwait(false) ? reg.EventName : ecr.ConsumerName;
        activity?.AddTag(ActivityTagNames.MessagingDestination, destination); // name of the queue/subscription
        activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue"); // the spec does not know subscription so we can only use queue for both

        Logger.ProcessingMessage(messageId: messageId, entityPath: entityPath);
        using var scope = CreateServiceScope(); // shared
        var contentType = new ContentType(message.ContentType);
        var context = await DeserializeAsync(scope: scope,
                                             body: message.Body,
                                             contentType: contentType,
                                             registration: reg,
                                             identifier: message.SequenceNumber.ToString(),
                                             raw: message,
                                             deadletter: ecr.Deadletter,
                                             cancellationToken: cancellationToken).ConfigureAwait(false);

        Logger.ReceivedMessage(sequenceNumber: message.SequenceNumber, eventBusId: context.Id, entityPath: entityPath);

        // set the extras
        context.SetServiceBusReceivedMessage(message);
        if (ecr.Deadletter && context is IDeadLetteredEventContext dlec)
        {
            dlec.DeadLetterReason = message.DeadLetterReason;
            dlec.DeadLetterErrorDescription = message.DeadLetterErrorDescription;
        }

        var (successful, ex) = await ConsumeAsync(scope, reg, ecr, context, cancellationToken).ConfigureAwait(false);
        if (ex != null)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);
        }

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
        // The processor will attempt to recover and if not, the processing stops and the owner of the
        // application should decide what to do since it shows up in the logs.
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

        if (successful) return autoComplete ? null : PostConsumeAction.Complete;

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
