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
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;

namespace Tingle.EventBus.Transports.Azure.EventHubs
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using Azure Event Hubs.
    /// </summary>
    [TransportName(TransportNames.AzureEventHubs)]
    public class AzureEventHubsTransport : EventBusTransportBase<AzureEventHubsTransportOptions>
    {
        private readonly Dictionary<(Type, bool), EventHubProducerClient> producersCache = new();
        private readonly SemaphoreSlim producersCacheLock = new(1, 1); // only one at a time.
        private readonly Dictionary<string, EventProcessorClient> processorsCache = new();
        private readonly SemaphoreSlim processorsCacheLock = new(1, 1); // only one at a time.

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
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            var registrations = GetRegistrations();
            foreach (var reg in registrations)
            {
                foreach (var ecr in reg.Consumers)
                {
                    var processor = await GetProcessorAsync(reg: reg, ecr: ecr, cancellationToken: cancellationToken);

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
                        var mt = GetType().GetMethod(nameof(OnEventReceivedAsync), flags) ?? throw new InvalidOperationException("Methods should be null");
                        var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);
                        return (Task)method.Invoke(this, new object[] { reg, ecr, processor, args, })!;
                    };

                    // start processing 
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
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken);

            var data = new EventData(body);
            data.Properties.AddIfNotDefault(MetadataNames.Id, @event.Id)
                           .AddIfNotDefault(MetadataNames.CorrelationId, @event.CorrelationId)
                           .AddIfNotDefault(MetadataNames.ContentType, @event.ContentType?.ToString())
                           .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                           .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                           .AddIfNotDefault(MetadataNames.EventName, registration.EventName)
                           .AddIfNotDefault(MetadataNames.EventType, registration.EventType.FullName)
                           .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

            // get the producer and send the event accordingly
            var producer = await GetProducerAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
            Logger.LogInformation("Sending {Id} to '{EventHubName}'. Scheduled: {Scheduled}",
                                  @event.Id,
                                  producer.EventHubName,
                                  scheduled);
            await producer.SendAsync(new[] { data }, cancellationToken);

            // return the sequence number
            return scheduled != null ? new ScheduledResult(id: data.SequenceNumber, scheduled: scheduled.Value) : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                 EventRegistration registration,
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
            foreach (var @event in events)
            {
                var body = await SerializeAsync(scope: scope,
                                                @event: @event,
                                                registration: registration,
                                                cancellationToken: cancellationToken);

                var data = new EventData(body);
                data.Properties.AddIfNotDefault(MetadataNames.Id, @event.Id)
                               .AddIfNotDefault(MetadataNames.CorrelationId, @event.CorrelationId)
                               .AddIfNotDefault(MetadataNames.ContentType, @event.ContentType?.ToString())
                               .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                               .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                               .AddIfNotDefault(MetadataNames.EventName, registration.EventName)
                               .AddIfNotDefault(MetadataNames.EventType, registration.EventType.FullName)
                               .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);
                datas.Add(data);
            }

            // get the producer and send the events accordingly
            var producer = await GetProducerAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
            Logger.LogInformation("Sending {EventsCount} events to '{EventHubName}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                                  events.Count,
                                  producer.EventHubName,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.Id)));
            await producer.SendAsync(datas, cancellationToken);

            // return the sequence numbers
            return scheduled != null ? datas.Select(m => new ScheduledResult(id: m.SequenceNumber, scheduled: scheduled.Value)).ToList() : null;
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Azure EventHubs does not support canceling published events.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
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

                    // Create the producer options
                    var epco = new EventHubProducerClientOptions
                    {
                        ConnectionOptions = new EventHubConnectionOptions
                        {
                            TransportType = TransportOptions.TransportType,
                        },
                    };

                    // Allow for the defaults to be overridden
                    TransportOptions.SetupProducerClientOptions?.Invoke(reg, epco);

                    // Override values that must be overridden

                    // Create the producer client
                    var cred = TransportOptions.Credentials!.Value!;
                    producer = cred is AzureEventHubsTransportCredentials aehtc
                            ? new EventHubProducerClient(fullyQualifiedNamespace: aehtc.FullyQualifiedNamespace,
                                                         eventHubName: name,
                                                         credential: aehtc.TokenCredential,
                                                         clientOptions: epco)
                            : new EventHubProducerClient(connectionString: (string)cred,
                                                         eventHubName: name,
                                                         clientOptions: epco);

                    // How to ensure event hub is created?
                    // EventHubs can only be create via Azure portal or using Resource Manager which need different credentials

                    producersCache[(reg.EventType, deadletter)] = producer;
                }

                return producer;
            }
            finally
            {
                producersCacheLock.Release();
            }
        }

        private async Task<EventProcessorClient> GetProcessorAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
        {
            await processorsCacheLock.WaitAsync(cancellationToken);

            try
            {
                var eventHubName = reg.EventName;
                var consumerGroup = TransportOptions.UseBasicTier ? EventHubConsumerClient.DefaultConsumerGroupName : ecr.ConsumerName;

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

                    // blobContainerUri has the format "https://{account_name}.blob.core.windows.net/{container_name}" which can be made using "{BlobServiceUri}/{container_name}".
                    var cred_bs = TransportOptions.BlobStorageCredentials!.Value!;
                    var blobContainerClient = cred_bs is AzureBlobStorageCredentials abstc
                        ? new BlobContainerClient(blobContainerUri: new Uri($"{abstc.BlobServiceUrl}/{TransportOptions.BlobContainerName}"),
                                                  credential: abstc.TokenCredential)
                        : new BlobContainerClient(connectionString: (string)cred_bs,
                                                                      blobContainerName: TransportOptions.BlobContainerName);

                    // Create the processor client options
                    var epco = new EventProcessorClientOptions
                    {
                        ConnectionOptions = new EventHubConnectionOptions
                        {
                            TransportType = TransportOptions.TransportType,
                        },
                    };

                    // Allow for the defaults to be overridden
                    TransportOptions.SetupProcessorClientOptions?.Invoke(reg, ecr, epco);

                    // How to ensure consumer is created in the event hub?
                    // EventHubs and ConsumerGroups can only be create via Azure portal or using Resource Manager which need different credentials


                    // Create the processor client
                    var cred = TransportOptions.Credentials!.Value!;
                    processor = cred is AzureEventHubsTransportCredentials aehtc
                        ? new EventProcessorClient(checkpointStore: blobContainerClient,
                                                         consumerGroup: consumerGroup,
                                                         fullyQualifiedNamespace: aehtc.FullyQualifiedNamespace,
                                                         eventHubName: eventHubName,
                                                         credential: aehtc.TokenCredential,
                                                         clientOptions: epco)
                        : new EventProcessorClient(checkpointStore: blobContainerClient,
                                                             consumerGroup: consumerGroup,
                                                             connectionString: (string)cred,
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

        private async Task OnEventReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                   EventConsumerRegistration ecr,
                                                                   EventProcessorClient processor,
                                                                   ProcessEventArgs args)
            where TEvent : class
            where TConsumer : IEventConsumer<TEvent>
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

            data.Properties.TryGetValue(MetadataNames.Id, out var eventId);
            data.Properties.TryGetValue(MetadataNames.CorrelationId, out var correlationId);
            data.Properties.TryGetValue(MetadataNames.ContentType, out var contentType_str);
            data.Properties.TryGetValue(MetadataNames.EventName, out var eventName);
            data.Properties.TryGetValue(MetadataNames.EventType, out var eventType);
            data.Properties.TryGetValue(MetadataNames.ActivityId, out var parentActivityId);

            using var log_scope = BeginLoggingScopeForConsume(id: eventId?.ToString(),
                                                              correlationId: correlationId?.ToString(),
                                                              sequenceNumber: data.SequenceNumber.ToString(),
                                                              extras: new Dictionary<string, string?>
                                                              {
                                                                  [MetadataNames.EventName] = eventName?.ToString(),
                                                                  [MetadataNames.EventType] = eventType?.ToString(),
                                                                  ["PartitionKey"] = data.PartitionKey,
                                                                  ["EventHubName"] = processor.EventHubName,
                                                                  ["ConsumerGroup"] = processor.ConsumerGroup,
                                                              });

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
            activity?.AddTag(ActivityTagNames.MessagingDestination, processor.EventHubName);

            Logger.LogDebug("Processing '{EventId}|{PartitionKey}|{SequenceNumber}' from '{EventHubName}/{ConsumerGroup}'",
                            eventId,
                            data.PartitionKey,
                            data.SequenceNumber,
                            processor.EventHubName,
                            processor.ConsumerGroup);
            using var scope = CreateScope();
            var contentType = contentType_str == null ? null : new ContentType(contentType_str.ToString());
            var context = await DeserializeAsync<TEvent>(scope: scope,
                                                         body: data.EventBody,
                                                         contentType: contentType,
                                                         registration: reg,
                                                         identifier: data.SequenceNumber.ToString(),
                                                         cancellationToken: cancellationToken);
            Logger.LogInformation("Received event: '{EventId}|{PartitionKey}|{SequenceNumber}' containing Event '{Id}' from '{EventHubName}/{ConsumerGroup}'",
                                  eventId,
                                  data.PartitionKey,
                                  data.SequenceNumber,
                                  context.Id,
                                  processor.EventHubName,
                                  processor.ConsumerGroup);

            // set the extras
            context.SetConsumerGroup(processor.ConsumerGroup)
                   .SetPartitionContext(args.Partition)
                   .SetEventData(data);

            var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(ecr: ecr,
                                                                        @event: context,
                                                                        scope: scope,
                                                                        cancellationToken: cancellationToken);

            if (!successful && ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
            {
                // get the producer for the dead letter event hub and send the event there
                var dlqProcessor = await GetProducerAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                await dlqProcessor.SendAsync(new[] { data }, cancellationToken);
            }

            /* 
             * Update the checkpoint store if needed so that the app receives
             * only newer events the next time it's run.
            */
            if (ShouldCheckpoint(successful, ecr.UnhandledErrorBehaviour))
            {
                Logger.LogDebug("Checkpointing {Partition} of '{EventHubName}/{ConsumerGroup}', at {SequenceNumber}. Event: '{Id}'.",
                                args.Partition,
                                processor.EventHubName,
                                processor.ConsumerGroup,
                                data.SequenceNumber,
                                eventId);
                await args.UpdateCheckpointAsync(args.CancellationToken);
            }
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

        internal static bool ShouldCheckpoint(bool successful, UnhandledConsumerErrorBehaviour? behaviour)
        {
            /*
             * We should only checkpoint if successful or we are discarding or deadlettering.
             * Otherwise the consumer should be allowed to handle the event again.
             * */
            return successful
                   || behaviour == UnhandledConsumerErrorBehaviour.Deadletter
                   || behaviour == UnhandledConsumerErrorBehaviour.Discard;
        }
    }
}
