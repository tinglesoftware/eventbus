using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
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

namespace Tingle.EventBus.Transports.Azure.EventHubs
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using Azure Event Hubs.
    /// </summary>
    public class AzureEventHubsEventBus : EventBusBase<AzureEventHubsOptions>
    {
        private readonly Dictionary<(Type, bool), EventHubProducerClient> producersCache = new Dictionary<(Type, bool), EventHubProducerClient>();
        private readonly SemaphoreSlim producersCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<string, EventProcessorClient> processorsCache = new Dictionary<string, EventProcessorClient>();
        private readonly SemaphoreSlim processorsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureEventHubsEventBus(IHostEnvironment environment,
                                      IServiceScopeFactory serviceScopeFactory,
                                      IOptions<EventBusOptions> busOptionsAccessor,
                                      IOptions<AzureEventHubsOptions> transportOptionsAccessor,
                                      ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            logger = loggerFactory?.CreateLogger<AzureEventHubsEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(EventBusHealthCheckExtras extras,
                                                          CancellationToken cancellationToken = default)
        {
            // get properties for the producers
            var producers = producersCache.Values;
            foreach (var proc in producers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await proc.GetEventHubPropertiesAsync(cancellationToken);
            }

            return true;
        }

        /// <inheritdoc/>
        protected override async Task StartBusAsync(CancellationToken cancellationToken)
        {
            var registrations = BusOptions.GetConsumerRegistrations();
            logger.StartingBusReceivers(registrations.Count);
            foreach (var reg in registrations)
            {
                var processor = await GetProcessorAsync(reg: reg, cancellationToken: cancellationToken);

                // register handlers for error and processing
                processor.PartitionClosingAsync += delegate (PartitionClosingEventArgs args)
                {
                    return OnPartitionClosingAsync(processor, args);
                };
                processor.PartitionInitializingAsync += delegate (PartitionInitializingEventArgs args)
                {
                    return OnPartitionInitializingAsync(processor, args);
                };
                processor.ProcessErrorAsync += delegate (ProcessErrorEventArgs args)
                {
                    return OnProcessErrorAsync(processor, args);
                };
                processor.ProcessEventAsync += delegate (ProcessEventArgs args)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var mt = GetType().GetMethod(nameof(OnEventReceivedAsync), flags);
                    var method = mt.MakeGenericMethod(reg.EventType, reg.ConsumerType);
                    return (Task)method.Invoke(this, new object[] { reg, processor, args, });
                };

                // start processing 
                await processor.StartProcessingAsync(cancellationToken: cancellationToken);
            }
        }

        /// <inheritdoc/>
        protected override async Task StopBusAsync(CancellationToken cancellationToken)
        {
            logger.StoppingBusReceivers();
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
        protected override async Task<string> PublishOnBusAsync<TEvent>(EventContext<TEvent> @event,
                                                                        DateTimeOffset? scheduled = null,
                                                                        CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                logger.LogWarning("Azure EventHubs does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   registration: reg,
                                                   scope: scope,
                                                   cancellationToken: cancellationToken);

            var message = new EventData(ms.ToArray());
            message.Properties["MessageId"] = @event.EventId;
            message.Properties["CorrelationId"] = @event.CorrelationId;
            message.Properties["Content-Type"] = contentType.ToString();

            // log warning when trying to publish expiring message
            if (@event.Expires != null)
            {
                logger.LogWarning("Azure EventHubs does not support expiring events");
            }

            // get the producer and send the message accordingly
            var producer = await GetProducerAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            await producer.SendAsync(new[] { message }, cancellationToken);

            // return the sequence number
            return message.SequenceNumber.ToString();
        }

        /// <inheritdoc/>
        protected override async Task<IList<string>> PublishOnBusAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                               DateTimeOffset? scheduled = null,
                                                                               CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                logger.LogWarning("Azure EventHubs does not support delay or scheduled publish");
            }

            // log warning when trying to publish expiring message
            if (events.Any(e => e.Expires != null))
            {
                logger.LogWarning("Azure EventHubs does not support expiring events");
            }

            using var scope = CreateScope();
            var messages = new List<EventData>();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       registration: reg,
                                                       scope: scope,
                                                       cancellationToken: cancellationToken);

                var message = new EventData(ms.ToArray());
                message.Properties["MessageId"] = @event.EventId;
                message.Properties["CorrelationId"] = @event.CorrelationId;
                message.Properties["Content-Type"] = contentType.ToString();
                messages.Add(message);
            }

            // get the producer and send the messages accordingly
            var producer = await GetProducerAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            await producer.SendAsync(messages, cancellationToken);

            // return the sequence numbers
            return messages.Select(m => m.SequenceNumber.ToString()).ToList();
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Azure EventHubs does not support canceling published messages.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Azure EventHubs does not support canceling published messages.");
        }

        private async Task<EventHubProducerClient> GetProducerAsync(EventRegistration reg, bool deadletter, CancellationToken cancellationToken)
        {
            await producersCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!producersCache.TryGetValue((reg.EventType, deadletter), out var producer))
                {
                    var name = reg.EventName;
                    if (deadletter) name += "-deadletter";

                    // create the producer
                    producer = new EventHubProducerClient(connectionString: TransportOptions.ConnectionString,
                                                          eventHubName: name,
                                                          clientOptions: new EventHubProducerClientOptions
                                                          {
                                                              ConnectionOptions = new EventHubConnectionOptions
                                                              {
                                                                  TransportType = TransportOptions.TransportType,
                                                              },
                                                          });

                    // ensure event hub is created

                    // EventHubs can only be create via Azure portal or using Resource Manager which needs different credentials
                    // TODO: consider optionally allowing for credentials to perform ARM operations?

                    producersCache[(reg.EventType, deadletter)] = producer;
                }

                return producer;
            }
            finally
            {
                producersCacheLock.Release();
            }
        }

        private async Task<EventProcessorClient> GetProcessorAsync(ConsumerRegistration reg, CancellationToken cancellationToken)
        {
            await processorsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var eventHubName = reg.EventName;
                var consumerGroup = TransportOptions.UseBasicTier ? EventHubConsumerClient.DefaultConsumerGroupName : reg.ConsumerName;

                var key = $"{eventHubName}/{consumerGroup}";
                if (!processorsCache.TryGetValue(key, out var processor))
                {
                    // if the EventHub or consumer group does not exist, create it

                    /*
                     * The publish/subscribe mechanism of Event Hubs is enabled through consumer groups.
                     * A consumer group is a view (state, position, or offset) of an entire event hub.
                     * Consumer groups enable multiple consuming applications to each have a separate view
                     * of the event stream, and to read the stream independently at their own pace and with
                     * their own offsets.
                     * 
                     * EventHubs and ConsumerGroups can only be create via Azure portal or using Resource Manager
                     * which needs different credentials.
                     */

                    // TODO: consider optionally allowing for credentials to perform ARM operations?

                    var blobContainerClient = new BlobContainerClient(connectionString: TransportOptions.BlobStorageConnectionString,
                                                                      blobContainerName: TransportOptions.BlobContainerName);

                    var epco = new EventProcessorClientOptions
                    {
                        ConnectionOptions = new EventHubConnectionOptions
                        {
                            TransportType = TransportOptions.TransportType,
                        },
                    };

                    // create the processor
                    processor = new EventProcessorClient(checkpointStore: blobContainerClient,
                                                         consumerGroup: consumerGroup,
                                                         connectionString: TransportOptions.ConnectionString,
                                                         eventHubName: eventHubName,
                                                         clientOptions: epco);
                    processorsCache[key] = processor;
                }

                return processor;
            }
            finally
            {
                processorsCacheLock.Release();
            }
        }

        private async Task OnEventReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg, EventProcessorClient processor, ProcessEventArgs args)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            if (!args.HasEvent)
            {
                logger.LogWarning($"'{nameof(OnEventReceivedAsync)}' was invoked but the arguments do not have an event.");
                return;
            }

            logger.LogDebug("Processor received event on EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, PartitionId:{PartitionId}",
                            processor.EventHubName,
                            processor.ConsumerGroup,
                            args.Partition.PartitionId);

            var data = args.Data;
            var cancellationToken = args.CancellationToken;

            data.Properties.TryGetValue("MessageId", out var messageId);
            data.Properties.TryGetValue("CorrelationId", out var correlationId);
            data.Properties.TryGetValue("Content-Type", out var contentType_str);

            using var log_scope = logger.BeginScope(new Dictionary<string, string>
            {
                ["MesageId"] = messageId?.ToString(),
                ["CorrelationId"] = correlationId?.ToString(),
                ["SequenceNumber"] = data.SequenceNumber.ToString(),
            });

            try
            {
                using var scope = CreateScope();
                using var ms = new MemoryStream(data.Body.ToArray());
                var contentType = new ContentType(contentType_str?.ToString() ?? "*/*");
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: reg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);
                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing failed. Moving to deadletter.");

                // get the producer for the dead letter event hub and send the event there
                var dlqProcessor = await GetProducerAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                await dlqProcessor.SendAsync(new[] { data }, cancellationToken);
            }

            // update the checkpoint store so that the app receives only new events the next time it's run
            await args.UpdateCheckpointAsync(args.CancellationToken);
        }

        private Task OnPartitionClosingAsync(EventProcessorClient processor, PartitionClosingEventArgs args)
        {
            logger.LogInformation("Closing processor for EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, PartitionId:{PartitionId} (Reason:{Reason})",
                                  processor.EventHubName,
                                  processor.ConsumerGroup,
                                  args.PartitionId,
                                  args.Reason);
            return Task.CompletedTask;
        }

        private Task OnPartitionInitializingAsync(EventProcessorClient processor, PartitionInitializingEventArgs args)
        {
            logger.LogInformation("Opening processor for PartitionId:{PartitionId}, EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, DefaultStartingPosition:{DefaultStartingPosition}",
                                  args.PartitionId,
                                  processor.EventHubName,
                                  processor.ConsumerGroup,
                                  args.DefaultStartingPosition);
            return Task.CompletedTask;
        }

        private Task OnProcessErrorAsync(EventProcessorClient processor, ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception,
                            "Event processing faulted. Operation:{Operation}, EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, PartitionId: {PartitionId}",
                            args.Operation,
                            processor.EventHubName,
                            processor.ConsumerGroup,
                            args.PartitionId);
            return Task.CompletedTask;
        }
    }
}
