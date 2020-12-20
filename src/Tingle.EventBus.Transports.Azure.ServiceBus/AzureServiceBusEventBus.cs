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
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Tingle.EventBus.Transports.Azure.ServiceBus
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using Azure Service Bus.
    /// </summary>
    public class AzureServiceBusEventBus : EventBusBase<AzureServiceBusOptions>
    {
        private readonly Dictionary<Type, ServiceBusSender> sendersCache = new Dictionary<Type, ServiceBusSender>();
        private readonly SemaphoreSlim sendersCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<string, ServiceBusProcessor> processorsCache = new Dictionary<string, ServiceBusProcessor>();
        private readonly SemaphoreSlim processorsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly ServiceBusAdministrationClient managementClient;
        private readonly ServiceBusClient serviceBusClient;
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureServiceBusEventBus(IHostEnvironment environment,
                                       IServiceScopeFactory serviceScopeFactory,
                                       IOptions<EventBusOptions> busOptionsAccessor,
                                       IOptions<AzureServiceBusOptions> transportOptionsAccessor,
                                       ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            var connectionString = TransportOptions.ConnectionStringProperties.ToString();
            managementClient = new ServiceBusAdministrationClient(connectionString);

            var sbcOptions = new ServiceBusClientOptions { TransportType = TransportOptions.TransportType, };
            serviceBusClient = new ServiceBusClient(connectionString, sbcOptions);

            logger = loggerFactory?.CreateLogger<AzureServiceBusEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var queues = managementClient.GetQueuesRuntimePropertiesAsync(cancellationToken).AsPages();
            await foreach (var _ in queues) ; // there's nothing to do
            var topics = managementClient.GetTopicsRuntimePropertiesAsync(cancellationToken);
            await foreach (var t in topics)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var subscriptions = managementClient.GetSubscriptionsRuntimePropertiesAsync(t.Name, cancellationToken);
                await foreach (var _ in subscriptions) ; // there's nothing to do
            }
            return true;
        }

        /// <inheritdoc/>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = BusOptions.GetRegistrations();
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
                await processor.StartProcessingAsync(cancellationToken: cancellationToken);
            }
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            var clients = processorsCache.Select(kvp => (key: kvp.Key, proc: kvp.Value)).ToList();
            foreach (var (key, proc) in clients)
            {
                logger.LogDebug("Stopping client: {Processor}", key);

                try
                {
                    await proc.StopProcessingAsync(cancellationToken);
                    processorsCache.Remove(key);

                    logger.LogDebug("Stopped processor for {Processor}", key);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Stop processor faulted for {Processor}", key);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            var reg = BusOptions.GetRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   serializerType: reg.EventSerializerType,
                                                   cancellationToken: cancellationToken);

            var message = new ServiceBusMessage(ms.ToArray())
            {
                MessageId = @event.EventId,
                CorrelationId = @event.CorrelationId,
                ContentType = contentType.ToString(),
            };

            // if scheduled for later, set the value in the message
            if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
            {
                message.ScheduledEnqueueTime = scheduled.Value.DateTime;
            }

            // if expiry is set in the future, set the ttl in the message
            if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
            {
                var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                message.TimeToLive = ttl;
            }

            // get the sender and send the message accordingly
            var sender = await GetSenderAsync(reg, cancellationToken);
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
            var messages = new List<ServiceBusMessage>();
            var reg = BusOptions.GetRegistration<TEvent>();
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       serializerType: reg.EventSerializerType,
                                                       cancellationToken: cancellationToken);

                var message = new ServiceBusMessage(ms.ToArray())
                {
                    MessageId = @event.EventId,
                    CorrelationId = @event.CorrelationId,
                    ContentType = contentType.ToString(),
                };

                // if scheduled for later, set the value in the message
                if (scheduled != null && scheduled > DateTimeOffset.UtcNow)
                {
                    message.ScheduledEnqueueTime = scheduled.Value.DateTime;
                }

                // if expiry is set in the future, set the ttl in the message
                if (@event.Expires != null && @event.Expires > DateTimeOffset.UtcNow)
                {
                    var ttl = @event.Expires.Value - DateTimeOffset.UtcNow;
                    message.TimeToLive = ttl;
                }

                messages.Add(message);
            }

            // get the sender and send the messages accordingly
            var sender = await GetSenderAsync(reg, cancellationToken);
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

        private async Task<ServiceBusSender> GetSenderAsync(ConsumerRegistration reg, CancellationToken cancellationToken)
        {
            await sendersCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!sendersCache.TryGetValue(reg.EventType, out var sender))
                {
                    var name = reg.EventName;

                    // ensure topic is created
                    await CreateTopicIfNotExistsAsync(topicName: name, cancellationToken: cancellationToken);

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
                    // if the subscription does not exist, create it
                    if (!await managementClient.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken))
                    {
                        // ensure topic is created before creating the subscription
                        await CreateTopicIfNotExistsAsync(topicName: topicName, cancellationToken: cancellationToken);

                        var options = new CreateSubscriptionOptions(topicName: topicName, subscriptionName: subscriptionName);

                        // TODO: set the defaults for a subscription here

                        // allow for the defaults to be overriden
                        TransportOptions.SetupSubscriptionOptions?.Invoke(options);
                        await managementClient.CreateSubscriptionAsync(options: options, cancellationToken: cancellationToken);
                    }

                    // create the processor
                    var sbpo = new ServiceBusProcessorOptions
                    {
                        // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                        // Set it according to how many messages the application wants to process in parallel.
                        MaxConcurrentCalls = 1,

                        // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                        // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                        AutoCompleteMessages = false,
                    };

                    processor = serviceBusClient.CreateProcessor(topicName: topicName,
                                                                 subscriptionName: subscriptionName,
                                                                 options: sbpo);
                    processorsCache[key] = processor;
                }

                return processor;
            }
            finally
            {
                processorsCacheLock.Release();
            }
        }

        private async Task CreateTopicIfNotExistsAsync(string topicName, CancellationToken cancellationToken)
        {
            // if the topic does not exist, create it
            if (!await managementClient.TopicExistsAsync(topicName, cancellationToken))
            {
                var options = new CreateTopicOptions(name: topicName);

                // TODO: set the defaults for a topic here

                // allow for the defaults to be overriden
                TransportOptions.SetupTopicOptions?.Invoke(options);
                _ = await managementClient.CreateTopicAsync(options: options, cancellationToken: cancellationToken);
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg, ProcessMessageEventArgs args)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            var message = args.Message;
            var cancellationToken = args.CancellationToken;

            using var log_scope = logger.BeginScope(new Dictionary<string, string>
            {
                ["MesageId"] = message.MessageId,
                ["CorrelationId"] = message.CorrelationId,
                ["SequenceNumber"] = message.SequenceNumber.ToString(),
                ["EnqueuedSequenceNumber"] = message.EnqueuedSequenceNumber.ToString(),
            });

            try
            {
                using var ms = message.Body.ToStream();
                var contentType = new ContentType(message.ContentType);
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             serializerType: reg.EventSerializerType,
                                                             cancellationToken: cancellationToken);
                await PushToConsumerAsync<TEvent, TConsumer>(context, cancellationToken);

                // complete the message
                await args.CompleteMessageAsync(message: message, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing failed. Moving to deadletter.");
                await args.DeadLetterMessageAsync(message: message, cancellationToken: cancellationToken);
            }
        }

        private Task OnMessageFaultedAsync(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception,
                            "Message receiving faulted. Endpoint:{Endpoint}, Entity Path: {EntityPath}, Source: {ErrorSource}",
                            args.FullyQualifiedNamespace,
                            args.EntityPath,
                            args.ErrorSource);
            return Task.CompletedTask;
        }
    }
}
