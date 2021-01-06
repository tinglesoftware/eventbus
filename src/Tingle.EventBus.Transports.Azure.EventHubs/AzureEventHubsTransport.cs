using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Azure.EventHubs
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using Azure Event Hubs.
    /// </summary>
    [TransportName(TransportNames.AzureEventHubs)]
    public class AzureEventHubsTransport : EventBusTransportBase<AzureEventHubsTransportOptions>
    {
        private readonly Dictionary<(Type, bool), EventHubProducerClient> producersCache = new Dictionary<(Type, bool), EventHubProducerClient>();
        private readonly SemaphoreSlim producersCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly Dictionary<string, EventProcessorClient> processorsCache = new Dictionary<string, EventProcessorClient>();
        private readonly SemaphoreSlim processorsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureEventHubsTransport(IServiceScopeFactory serviceScopeFactory,
                                       IOptions<EventBusOptions> busOptionsAccessor,
                                       IOptions<AzureEventHubsTransportOptions> transportOptionsAccessor,
                                       ILoggerFactory loggerFactory)
            : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(Dictionary<string, object> data,
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
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = GetConsumerRegistrations();
            Logger.StartingTransport(registrations.Count);
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
            // log warning when trying to publish scheduled event
            if (scheduled != null)
            {
                Logger.LogWarning("Azure EventHubs does not support delay or scheduled publish");
            }

            // log warning when trying to publish expiring event
            if (@event.Expires != null)
            {
                Logger.LogWarning("Azure EventHubs does not support expiring events");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            await SerializeAsync(body: ms,
                                 @event: @event,
                                 registration: reg,
                                 scope: scope,
                                 cancellationToken: cancellationToken);

            var data = new EventData(ms.ToArray());
            data.Properties.AddIfNotDefault(AttributeNames.Id, @event.Id)
                           .AddIfNotDefault(AttributeNames.CorrelationId, @event.CorrelationId)
                           .AddIfNotDefault(AttributeNames.ContentType, @event.ContentType.ToString())
                           .AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                           .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                           .AddIfNotDefault(AttributeNames.EventName, reg.EventName)
                           .AddIfNotDefault(AttributeNames.EventType, reg.EventType.FullName)
                           .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);

            // get the producer and send the event accordingly
            var producer = await GetProducerAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            Logger.LogInformation("Sending {Id} to '{EventHubName}'. Scheduled: {Scheduled}",
                                  @event.Id,
                                  producer.EventHubName,
                                  scheduled);
            await producer.SendAsync(new[] { data }, cancellationToken);

            // return the sequence number
            return data.SequenceNumber.ToString();
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled events
            if (scheduled != null)
            {
                Logger.LogWarning("Azure EventHubs does not support delay or scheduled publish");
            }

            // log warning when trying to publish expiring events
            if (events.Any(e => e.Expires != null))
            {
                Logger.LogWarning("Azure EventHubs does not support expiring events");
            }

            using var scope = CreateScope();
            var datas = new List<EventData>();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                await SerializeAsync(body: ms,
                                     @event: @event,
                                     registration: reg,
                                     scope: scope,
                                     cancellationToken: cancellationToken);

                var data = new EventData(ms.ToArray());
                data.Properties.AddIfNotDefault(AttributeNames.Id, @event.Id)
                               .AddIfNotDefault(AttributeNames.CorrelationId, @event.CorrelationId)
                               .AddIfNotDefault(AttributeNames.ContentType, @event.ContentType.ToString())
                               .AddIfNotDefault(AttributeNames.RequestId, @event.RequestId)
                               .AddIfNotDefault(AttributeNames.InitiatorId, @event.InitiatorId)
                               .AddIfNotDefault(AttributeNames.EventName, reg.EventName)
                               .AddIfNotDefault(AttributeNames.EventType, reg.EventType.FullName)
                               .AddIfNotDefault(AttributeNames.ActivityId, Activity.Current?.Id);
                datas.Add(data);
            }

            // get the producer and send the events accordingly
            var producer = await GetProducerAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            Logger.LogInformation("Sending {EventsCount} events to '{EventHubName}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                                  events.Count,
                                  producer.EventHubName,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.Id)));
            await producer.SendAsync(datas, cancellationToken);

            // return the sequence numbers
            return datas.Select(m => m.SequenceNumber.ToString()).ToList();
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Azure EventHubs does not support canceling published events.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Azure EventHubs does not support canceling published events.");
        }

        private async Task<EventHubProducerClient> GetProducerAsync(EventRegistration reg, bool deadletter, CancellationToken cancellationToken)
        {
            await producersCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!producersCache.TryGetValue((reg.EventType, deadletter), out var producer))
                {
                    var name = reg.EventName;
                    if (deadletter) name += TransportOptions.DeadLetterSuffix;

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
                Logger.LogWarning($"'{nameof(OnEventReceivedAsync)}' was invoked but the arguments do not have an event.");
                return;
            }

            Logger.LogDebug("Processor received event on EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, PartitionId:{PartitionId}",
                            processor.EventHubName,
                            processor.ConsumerGroup,
                            args.Partition.PartitionId);

            var data = args.Data;
            var cancellationToken = args.CancellationToken;

            data.Properties.TryGetValue(AttributeNames.Id, out var eventId);
            data.Properties.TryGetValue(AttributeNames.CorrelationId, out var correlationId);
            data.Properties.TryGetValue(AttributeNames.ContentType, out var contentType_str);
            data.Properties.TryGetValue(AttributeNames.EventName, out var eventName);
            data.Properties.TryGetValue(AttributeNames.EventType, out var eventType);
            data.Properties.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

            using var log_scope = Logger.BeginScopeForConsume(id: eventId?.ToString(),
                                                              correlationId: correlationId?.ToString(),
                                                              sequenceNumber: data.SequenceNumber,
                                                              extras: new Dictionary<string, string>
                                                              {
                                                                  [AttributeNames.EventName] = eventName?.ToString(),
                                                                  [AttributeNames.EventType] = eventType?.ToString(),
                                                              });

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
            activity?.AddTag(ActivityTagNames.MessagingDestination, processor.EventHubName);

            try
            {
                Logger.LogDebug("Processing '{EventId}|{PartitionKey}|{SequenceNumber}'",
                                eventId,
                                data.PartitionKey,
                                data.SequenceNumber);
                using var scope = CreateScope();
                using var ms = new MemoryStream(data.Body.ToArray());
                var contentType = contentType_str == null ? null : new ContentType(contentType_str.ToString());
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: reg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);
                Logger.LogInformation("Received event: '{EventId}|{PartitionKey}|{SequenceNumber}' containing Event '{Id}'",
                                      eventId,
                                      data.PartitionKey,
                                      data.SequenceNumber,
                                      context.Id);

                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event processing failed. Moving to deadletter.");

                // get the producer for the dead letter event hub and send the event there
                var dlqProcessor = await GetProducerAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                await dlqProcessor.SendAsync(new[] { data }, cancellationToken);
            }

            // update the checkpoint store so that the app receives only new events the next time it's run
            Logger.LogDebug("Checkpointing {PartitionKey}, at {SequenceNumber}. Event: '{Id}'.",
                            args.Partition,
                            data.SequenceNumber,
                            eventId);
            await args.UpdateCheckpointAsync(args.CancellationToken);
        }

        private Task OnPartitionClosingAsync(EventProcessorClient processor, PartitionClosingEventArgs args)
        {
            Logger.LogInformation("Closing processor for EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, PartitionId:{PartitionId} (Reason:{Reason})",
                                  processor.EventHubName,
                                  processor.ConsumerGroup,
                                  args.PartitionId,
                                  args.Reason);
            return Task.CompletedTask;
        }

        private Task OnPartitionInitializingAsync(EventProcessorClient processor, PartitionInitializingEventArgs args)
        {
            Logger.LogInformation("Opening processor for PartitionId:{PartitionId}, EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, DefaultStartingPosition:{DefaultStartingPosition}",
                                  args.PartitionId,
                                  processor.EventHubName,
                                  processor.ConsumerGroup,
                                  args.DefaultStartingPosition);
            return Task.CompletedTask;
        }

        private Task OnProcessErrorAsync(EventProcessorClient processor, ProcessErrorEventArgs args)
        {
            Logger.LogError(args.Exception,
                            "Event processing faulted. Operation:{Operation}, EventHub:{EventHubName}, ConsumerGroup:{ConsumerGroup}, PartitionId: {PartitionId}",
                            args.Operation,
                            processor.EventHubName,
                            processor.ConsumerGroup,
                            args.PartitionId);
            return Task.CompletedTask;
        }
    }
}
