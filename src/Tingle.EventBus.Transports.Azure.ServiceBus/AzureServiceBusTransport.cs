using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
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
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Azure.ServiceBus
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using Azure Service Bus.
    /// </summary>
    [TransportName(TransportNames.AzureServiceBus)]
    public class AzureServiceBusTransport : EventBusTransportBase<AzureServiceBusOptions>
    {
        private readonly Dictionary<Type, ServiceBusSender> sendersCache = new Dictionary<Type, ServiceBusSender>();
        private readonly SemaphoreSlim sendersCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<string, ServiceBusProcessor> processorsCache = new Dictionary<string, ServiceBusProcessor>();
        private readonly SemaphoreSlim processorsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly ServiceBusAdministrationClient managementClient;
        private readonly ServiceBusClient serviceBusClient;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureServiceBusTransport(IHostEnvironment environment,
                                        IServiceScopeFactory serviceScopeFactory,
                                        IOptions<EventBusOptions> busOptionsAccessor,
                                        IOptions<AzureServiceBusOptions> transportOptionsAccessor,
                                        ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            var connectionString = TransportOptions.ConnectionString;
            managementClient = new ServiceBusAdministrationClient(connectionString);

            var sbcOptions = new ServiceBusClientOptions { TransportType = TransportOptions.TransportType, };
            serviceBusClient = new ServiceBusClient(connectionString, sbcOptions);
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(EventBusHealthCheckExtras extras,
                                                          CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Listing Queues ...");
            var queues = managementClient.GetQueuesRuntimePropertiesAsync(cancellationToken).AsPages();
            await foreach (var _ in queues) ; // there's nothing to do
            if (!TransportOptions.UseBasicTier)
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
            var registrations = GetConsumerRegistrations();
            Logger.StartingTransport(registrations.Count);
            foreach (var reg in registrations)
            {
                var processor = await GetProcessorAsync(reg: reg, cancellationToken: cancellationToken);

                // register handlers for error and processing
                processor.ProcessErrorAsync += OnMessageFaultedAsync;
                processor.ProcessMessageAsync += delegate (ProcessMessageEventArgs args)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
                    var method = mt.MakeGenericMethod(reg.EventType, reg.ConsumerType);
                    return (Task)method.Invoke(this, new object[] { reg, args, });
                };

                // start processing
                Logger.LogInformation("Starting processing on {EntityPath}", processor.EntityPath);
                await processor.StartProcessingAsync(cancellationToken: cancellationToken);
            }
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.StoppingTransport();
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
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   registration: reg,
                                                   scope: scope,
                                                   cancellationToken: cancellationToken);

            var message = new ServiceBusMessage(ms.ToArray())
            {
                MessageId = @event.EventId,
                ContentType = contentType?.ToString(),
            };

            // If CorrelationId is present, set it
            if (@event.CorrelationId != null)
            {
                message.CorrelationId = @event.CorrelationId;
            }

            // If scheduled for later, set the value in the message
            if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
            {
                message.ScheduledEnqueueTime = scheduled.Value.DateTime;
            }

            // If expiry is set in the future, set the ttl in the message
            if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
            {
                var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                message.TimeToLive = ttl;
            }

            // Get the sender and send the message accordingly
            var sender = await GetSenderAsync(reg, cancellationToken);
            Logger.LogInformation("Sending {EventId} to '{EntityPath}'. Scheduled: {Scheduled}",
                                  @event.EventId,
                                  sender.EntityPath,
                                  scheduled);
            if (scheduled != null)
            {
                var seqNum = await sender.ScheduleMessageAsync(message: message,
                                                               scheduledEnqueueTime: message.ScheduledEnqueueTime,
                                                               cancellationToken: cancellationToken);
                return seqNum.ToString(); // return the sequence number
            }
            else
            {
                await sender.SendMessageAsync(message, cancellationToken);
                return null; // no sequence number available
            }
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        {
            using var scope = CreateScope();
            var messages = new List<ServiceBusMessage>();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       registration: reg,
                                                       scope: scope,
                                                       cancellationToken: cancellationToken);

                var message = new ServiceBusMessage(ms.ToArray())
                {
                    MessageId = @event.EventId,
                    CorrelationId = @event.CorrelationId,
                    ContentType = contentType.ToString(),
                };

                // If CorrelationId is present, set it
                if (@event.CorrelationId != null)
                {
                    message.CorrelationId = @event.CorrelationId;
                }

                // If scheduled for later, set the value in the message
                if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
                {
                    message.ScheduledEnqueueTime = scheduled.Value.DateTime;
                }

                // If expiry is set in the future, set the ttl in the message
                if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
                {
                    var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                    message.TimeToLive = ttl;
                }

                messages.Add(message);
            }

            // Get the sender and send the messages accordingly
            var sender = await GetSenderAsync(reg, cancellationToken);
            Logger.LogInformation("Sending {EventsCount} messages to '{EntityPath}'. Scheduled: {Scheduled}. Events:\r\n- {EventIds}",
                                  events.Count,
                                  sender.EntityPath,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.EventId)));
            if (scheduled != null)
            {
                var seqNums = await sender.ScheduleMessagesAsync(messages: messages,
                                                                 scheduledEnqueueTime: messages.First().ScheduledEnqueueTime,
                                                                 cancellationToken: cancellationToken);
                return seqNums.Select(sn => sn.ToString()).ToList(); // return the sequence numbers
            }
            else
            {
                await sender.SendMessagesAsync(messages: messages, cancellationToken: cancellationToken);
                return Array.Empty<string>(); // no sequence numbers available
            }
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
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
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var sender = await GetSenderAsync(reg, cancellationToken);
            Logger.LogInformation("Canceling scheduled message: {SequenceNumber} on {EntityPath}", seqNum, sender.EntityPath);
            await sender.CancelScheduledMessageAsync(sequenceNumber: seqNum, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
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
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var sender = await GetSenderAsync(reg, cancellationToken);
            Logger.LogInformation("Canceling scheduled messages on {EntityPath}:\r\n- {SequenceNumbers}",
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
                    var name = reg.EventName;

                    if (TransportOptions.UseBasicTier)
                    {
                        // ensure queue is created, for basic tier
                        Logger.LogDebug("Creating sender for queue '{QueueName}'", name);
                        await CreateQueueIfNotExistsAsync(name: name, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // ensure topic is created, for non-basic tier
                        Logger.LogDebug("Creating sender for topic '{TopicName}'", name);
                        await CreateTopicIfNotExistsAsync(name: name, cancellationToken: cancellationToken);
                    }

                    // create the sender
                    sender = serviceBusClient.CreateSender(name);
                    sendersCache[reg.EventType] = sender;
                }

                return sender;
            }
            finally
            {
                sendersCacheLock.Release();
            }
        }

        private async Task<ServiceBusProcessor> GetProcessorAsync(ConsumerRegistration reg, CancellationToken cancellationToken)
        {
            await processorsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var topicName = reg.EventName;
                var subscriptionName = reg.ConsumerName;

                var key = $"{topicName}/{subscriptionName}";
                if (!processorsCache.TryGetValue(key, out var processor))
                {
                    if (TransportOptions.UseBasicTier)
                    {
                        // Ensure queue is created for basic tier
                        await CreateQueueIfNotExistsAsync(name: topicName, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Ensure topic is created before creating the subscription, for non-basic tier
                        await CreateTopicIfNotExistsAsync(name: topicName, cancellationToken: cancellationToken);

                        // Ensure subscription is created
                        await CreateSubscriptionIfNotExistsAsync(topicName: topicName,
                                                                subscriptionName: subscriptionName,
                                                                cancellationToken: cancellationToken);
                    }

                    // Create the processor
                    var sbpo = new ServiceBusProcessorOptions
                    {
                        // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                        // Set it according to how many messages the application wants to process in parallel.
                        MaxConcurrentCalls = 1,

                        // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                        // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                        AutoCompleteMessages = false,
                    };

                    if (TransportOptions.UseBasicTier)
                    {
                        Logger.LogDebug("Creating processor for queue '{QueueName}'", topicName);
                        processor = serviceBusClient.CreateProcessor(queueName: topicName, options: sbpo);
                    }
                    else
                    {
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

        private async Task CreateQueueIfNotExistsAsync(string name, CancellationToken cancellationToken)
        {
            // if entity creation is not enabled, just return
            if (!TransportOptions.EnableEntityCreation)
            {
                Logger.LogTrace("Entity creation is diabled. Queue creation skipped");
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
                    MaxDeliveryCount = 10,
                };

                // Allow for the defaults to be overriden
                TransportOptions.SetupQueueOptions?.Invoke(options);
                Logger.LogInformation("Creating queue '{QueueName}'", name);
                _ = await managementClient.CreateQueueAsync(options: options, cancellationToken: cancellationToken);
            }
        }

        private async Task CreateTopicIfNotExistsAsync(string name, CancellationToken cancellationToken)
        {
            // if entity creation is not enabled, just return
            if (!TransportOptions.EnableEntityCreation)
            {
                Logger.LogTrace("Entity creation is diabled. Topic creation skipped");
                return;
            }

            // If the topic does not exist, create it
            Logger.LogDebug("Checking if topic '{TopicName}' exists", name);
            if (!await managementClient.TopicExistsAsync(name: name, cancellationToken: cancellationToken))
            {
                Logger.LogTrace("Topic '{TopicName}' does not exist, preparing creation.", name);
                var options = new CreateTopicOptions(name: name)
                {
                    // set the defaults for a topic here
                    Status = EntityStatus.Active,
                    EnablePartitioning = false,
                    RequiresDuplicateDetection = BusOptions.EnableDeduplication,
                    DuplicateDetectionHistoryTimeWindow = BusOptions.DuplicateDetectionDuration,
                };

                // Allow for the defaults to be overriden
                TransportOptions.SetupTopicOptions?.Invoke(options);
                Logger.LogInformation("Creating topic '{TopicName}'", name);
                _ = await managementClient.CreateTopicAsync(options: options, cancellationToken: cancellationToken);
            }
        }

        private async Task CreateSubscriptionIfNotExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken)
        {
            // if entity creation is not enabled, just return
            if (!TransportOptions.EnableEntityCreation)
            {
                Logger.LogTrace("Entity creation is diabled. Subscription creation skipped");
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
                    // set the defaults for a subscription here
                    Status = EntityStatus.Active,
                    MaxDeliveryCount = 10,
                };

                // Allow for the defaults to be overriden
                TransportOptions.SetupSubscriptionOptions?.Invoke(options);
                Logger.LogInformation("Creating subscription '{SubscriptionName}' under topic '{TopicName}'",
                                      subscriptionName,
                                      topicName);
                await managementClient.CreateSubscriptionAsync(options: options, cancellationToken: cancellationToken);
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg, ProcessMessageEventArgs args)
                where TEvent : class
                where TConsumer : IEventBusConsumer<TEvent>
        {
            var message = args.Message;
            var cancellationToken = args.CancellationToken;

            using var log_scope = Logger.BeginScope(new Dictionary<string, string>
            {
                ["MesageId"] = message.MessageId,
                ["CorrelationId"] = message.CorrelationId,
                ["SequenceNumber"] = message.SequenceNumber.ToString(),
                ["EnqueuedSequenceNumber"] = message.EnqueuedSequenceNumber.ToString(),
            });

            try
            {
                using var scope = CreateScope();
                using var ms = message.Body.ToStream();
                var contentType = new ContentType(message.ContentType);
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: reg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);
                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);

                // Complete the message
                Logger.LogDebug("Completing message: {MessageId}.", message.MessageId);
                await args.CompleteMessageAsync(message: message, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event processing failed. Moving to deadletter.");
                await args.DeadLetterMessageAsync(message: message, cancellationToken: cancellationToken);
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
    }
}
