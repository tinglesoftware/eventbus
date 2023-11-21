using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports.Azure.EventHubs;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using Azure Event Hubs.
/// </summary>
public class AzureEventHubsTransport : EventBusTransport<AzureEventHubsTransportOptions>
{
    private readonly EventBusConcurrentDictionary<(Type, bool), EventHubProducerClient> producersCache = new();
    private readonly EventBusConcurrentDictionary<string, EventProcessorClient> processorsCache = new();
    private readonly SemaphoreSlim blobContainerClientLock = new(1, 1); // only one at a time.
    private readonly ConcurrentDictionary<string, int> checkpointingCounter = new();
    private BlobContainerClient? blobContainerClient;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public AzureEventHubsTransport(IServiceScopeFactory serviceScopeFactory,
                                   IOptions<EventBusOptions> busOptionsAccessor,
                                   IOptionsMonitor<AzureEventHubsTransportOptions> optionsMonitor,
                                   ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory) { }

    /// <inheritdoc/>
    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            foreach (var ecr in reg.Consumers)
            {
                var processor = await GetProcessorAsync(reg: reg, ecr: ecr, cancellationToken: cancellationToken).ConfigureAwait(false);

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
                await processor.StartProcessingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var clients = processorsCache.Select(kvp => (key: kvp.Key, proc: kvp.Value)).ToList();
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
    protected override async Task<ScheduledResult?> PublishCoreAsync<TEvent>(EventContext<TEvent> @event,
                                                                             EventRegistration registration,
                                                                             DateTimeOffset? scheduled = null,
                                                                             CancellationToken cancellationToken = default)
    {
        // log warning when trying to publish scheduled event
        if (scheduled != null)
        {
            Logger.SchedulingNotSupported();
        }

        // log warning when trying to publish expiring event
        if (@event.Expires != null)
        {
            Logger.ExpiryNotSupported();
        }

        using var scope = CreateScope();
        var body = await SerializeAsync(scope: scope,
                                        @event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

        var data = new EventData(body)
        {
            MessageId = @event.Id,
            ContentType = @event.ContentType?.ToString(),
        };

        // If CorrelationId is present, set it
        if (@event.CorrelationId != null)
        {
            data.CorrelationId = @event.CorrelationId;
        }

        data.Properties.ToEventBusWrapper()
                       .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                       .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                       .AddIfNotDefault(MetadataNames.EventName, registration.EventName)
                       .AddIfNotDefault(MetadataNames.EventType, registration.EventType.FullName)
                       .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);

        // get the producer and send the event accordingly
        var producer = await GetProducerAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.SendingEvent(eventBusId: @event.Id, eventHubName: producer.EventHubName, scheduled: scheduled);
        await producer.SendAsync(new[] { data }, cancellationToken).ConfigureAwait(false);

        // return the sequence number
        return scheduled != null ? new ScheduledResult(id: data.SequenceNumber, scheduled: scheduled.Value) : null;
    }

    /// <inheritdoc/>
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                    EventRegistration registration,
                                                                                    DateTimeOffset? scheduled = null,
                                                                                    CancellationToken cancellationToken = default)
    {
        // log warning when trying to publish scheduled events
        if (scheduled != null)
        {
            Logger.SchedulingNotSupported();
        }

        // log warning when trying to publish expiring events
        if (events.Any(e => e.Expires != null))
        {
            Logger.ExpiryNotSupported();
        }

        using var scope = CreateScope();
        var datas = new List<EventData>();
        foreach (var @event in events)
        {
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);

            var data = new EventData(body)
            {
                MessageId = @event.Id,
                ContentType = @event.ContentType?.ToString(),
            };

            // If CorrelationId is present, set it
            if (@event.CorrelationId != null)
            {
                data.CorrelationId = @event.CorrelationId;
            }

            data.Properties.ToEventBusWrapper()
                           .AddIfNotDefault(MetadataNames.RequestId, @event.RequestId)
                           .AddIfNotDefault(MetadataNames.InitiatorId, @event.InitiatorId)
                           .AddIfNotDefault(MetadataNames.EventName, registration.EventName)
                           .AddIfNotDefault(MetadataNames.EventType, registration.EventType.FullName)
                           .AddIfNotDefault(MetadataNames.ActivityId, Activity.Current?.Id);
            datas.Add(data);
        }

        // get the producer and send the events accordingly
        var producer = await GetProducerAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.SendingEvents(events: events, eventHubName: producer.EventHubName, scheduled: scheduled);
        await producer.SendAsync(datas, cancellationToken).ConfigureAwait(false);

        // return the sequence numbers
        return scheduled != null ? datas.Select(m => new ScheduledResult(id: m.SequenceNumber, scheduled: scheduled.Value)).ToList() : null;
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(string id,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Azure EventHubs does not support canceling published events.");
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(IList<string> ids,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Azure EventHubs does not support canceling published events.");
    }

    private async Task<BlobContainerClient> GetBlobContainerClientAsync(CancellationToken cancellationToken)
    {
        await blobContainerClientLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (blobContainerClient is null)
            {
                var options = Options.SetupBlobClientOptions();

                // blobContainerUri has the format "https://{account_name}.blob.core.windows.net/{container_name}" which can be made using "{BlobServiceUri}/{container_name}".
                var cred_bs = Options.BlobStorageCredentials.CurrentValue;
                var blobServiceClient = cred_bs is AzureBlobStorageCredentials abstc
                    ? new BlobServiceClient(serviceUri: abstc.ServiceUrl, credential: abstc.TokenCredential, options: options)
                    : new BlobServiceClient(connectionString: (string)cred_bs, options: options);
                blobContainerClient = blobServiceClient.GetBlobContainerClient(Options.BlobContainerName);
                Logger.CheckpointStore(blobContainerClient.Uri);

                // Create the blob container if it does not exist
                if (Options.EnableEntityCreation)
                {
                    await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            blobContainerClientLock.Release();
        }

        return blobContainerClient;
    }

    private Task<EventHubProducerClient> GetProducerAsync(EventRegistration reg, bool deadletter, CancellationToken cancellationToken)
    {
        Task<EventHubProducerClient> creator((Type, bool) _, CancellationToken ct)
        {
            var name = reg.EventName;
            if (deadletter) name += Options.DeadLetterSuffix;

            // Create the producer options
            var epco = new EventHubProducerClientOptions
            {
                ConnectionOptions = new EventHubConnectionOptions
                {
                    TransportType = Options.TransportType,
                },
            };

            // Allow for the defaults to be overridden
            Options.SetupProducerClientOptions?.Invoke(reg, epco);

            // Override values that must be overridden

            // Create the producer client
            var cred = Options.Credentials.CurrentValue;
            var producer = cred is AzureEventHubsTransportCredentials aehtc
                    ? new EventHubProducerClient(fullyQualifiedNamespace: aehtc.FullyQualifiedNamespace,
                                                 eventHubName: name,
                                                 credential: aehtc.TokenCredential,
                                                 clientOptions: epco)
                    : new EventHubProducerClient(connectionString: (string)cred,
                                                 eventHubName: name,
                                                 clientOptions: epco);

            // How to ensure event hub is created?
            // EventHubs can only be create via Azure portal or using Resource Manager which need different credentials

            return Task.FromResult(producer);
        }
        return producersCache.GetOrAddAsync((reg.EventType, deadletter), creator, cancellationToken);
    }

    private Task<EventProcessorClient> GetProcessorAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
    {
        var name = reg.EventName;
        var deadletter = ecr.Deadletter;
        if (deadletter) name += Options.DeadLetterSuffix;

        // For events configured as sourced from IoT Hub,
        // 1. The event hub name is in the metadata
        // 2. The ConsumerGroup is set to $Default (this may be changed to support more)
        var isIotHub = reg.IsConfiguredAsIotHub();
        var eventHubName = isIotHub ? reg.GetIotHubEventHubName() : name;
        var consumerGroup = Options.UseBasicTier ? EventHubConsumerClient.DefaultConsumerGroupName : ecr.ConsumerName;

        async Task<EventProcessorClient> creator(string key, CancellationToken ct)
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

            var blobContainerClient = await GetBlobContainerClientAsync(cancellationToken).ConfigureAwait(false);

            // Create the processor client options
            var epco = new EventProcessorClientOptions
            {
                ConnectionOptions = new EventHubConnectionOptions
                {
                    TransportType = Options.TransportType,
                },
            };

            // Allow for the defaults to be overridden
            Options.SetupProcessorClientOptions?.Invoke(reg, ecr, epco);

            // How to ensure consumer is created in the event hub?
            // EventHubs and ConsumerGroups can only be create via Azure portal or using Resource Manager which need different credentials


            // Create the processor client
            var cred = Options.Credentials.CurrentValue;
            var processor = cred is AzureEventHubsTransportCredentials aehtc
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

            return processor;
        }

        var key = $"{eventHubName}/{consumerGroup}/{deadletter}";
        return processorsCache.GetOrAddAsync(key, creator, cancellationToken);
    }

    private async Task OnEventReceivedAsync<TEvent, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] TConsumer>(EventRegistration reg,
                                                                                                                 EventConsumerRegistration ecr,
                                                                                                                 EventProcessorClient processor,
                                                                                                                 ProcessEventArgs args)
        where TEvent : class
        where TConsumer : IEventConsumer
    {
        Logger.ProcessorReceivedEvent(eventHubName: processor.EventHubName,
                                      consumerGroup: processor.ConsumerGroup,
                                      partitionId: args.Partition.PartitionId);

        var cancellationToken = args.CancellationToken;
        var partitionId = args.Partition.PartitionId;
        var data = args.Data;
        var messageId = data.MessageId;

        data.TryGetPropertyValue<string>(MetadataNames.EventName, out var eventName);
        data.TryGetPropertyValue<string>(MetadataNames.EventType, out var eventType);
        data.TryGetPropertyValue<string>(MetadataNames.ActivityId, out var parentActivityId);

        using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                          correlationId: data.CorrelationId,
                                                          sequenceNumber: data.SequenceNumber.ToString(),
                                                          extras: new Dictionary<string, string?>
                                                          {
                                                              [MetadataNames.EventName] = eventName?.ToString(),
                                                              [MetadataNames.EventType] = eventType?.ToString(),
                                                              ["PartitionId"] = partitionId,
                                                              ["EventHubName"] = processor.EventHubName,
                                                              ["ConsumerGroup"] = processor.ConsumerGroup,
                                                              ["PartitionKey"] = data.PartitionKey,
                                                          });

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
        activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
        activity?.AddTag(ActivityTagNames.MessagingDestination, processor.EventHubName);

        Logger.ProcessingEvent(messageId: messageId,
                               eventHubName: processor.EventHubName,
                               consumerGroup: processor.ConsumerGroup,
                               partitionId: partitionId,
                               partitionKey: data.PartitionKey,
                               sequenceNumber: data.SequenceNumber);
        using var scope = CreateScope();
        var contentType = new ContentType(data.ContentType);
        var context = await DeserializeAsync<TEvent>(scope: scope,
                                                     body: data.EventBody,
                                                     contentType: contentType,
                                                     registration: reg,
                                                     identifier: data.SequenceNumber.ToString(),
                                                     raw: data,
                                                     deadletter: ecr.Deadletter,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.ReceivedEvent(eventBusId: context.Id,
                             eventHubName: processor.EventHubName,
                             consumerGroup: processor.ConsumerGroup,
                             partitionId: partitionId,
                             partitionKey: data.PartitionKey,
                             sequenceNumber: data.SequenceNumber);

        // set the extras
        context.SetConsumerGroup(processor.ConsumerGroup)
               .SetPartitionContext(args.Partition)
               .SetEventData(data);

        var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(registration: reg,
                                                                    ecr: ecr,
                                                                    @event: context,
                                                                    scope: scope,
                                                                    cancellationToken: cancellationToken).ConfigureAwait(false);

        // dead-letter cannot be dead-lettered again, what else can we do?
        if (ecr.Deadletter) return; // TODO: figure out what to do when dead-letter fails

        if (!successful && ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
        {
            // get the producer for the dead letter event hub and send the event there
            var dlqProcessor = await GetProducerAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            await dlqProcessor.SendAsync(new[] { data }, cancellationToken).ConfigureAwait(false);
        }

        /* 
         * Update the checkpoint store if needed so that the app receives
         * only newer events the next time it's run.
        */
        if (CanCheckpoint(successful, ecr.UnhandledErrorBehaviour))
        {
            var counterKey = string.Join("|", processor.EventHubName, processor.ConsumerGroup, partitionId);
            var countSinceLast = checkpointingCounter.AddOrUpdate(key: counterKey,
                                                                  addValue: 1,
                                                                  updateValueFactory: (_, current) => current + 1);
            if (countSinceLast >= Options.CheckpointInterval)
            {
                Logger.Checkpointing(partitionId: partitionId,
                                     eventHubName: processor.EventHubName,
                                     consumerGroup: processor.ConsumerGroup,
                                     sequenceNumber: data.SequenceNumber);
                await args.UpdateCheckpointAsync(cancellationToken).ConfigureAwait(false);
                checkpointingCounter[counterKey] = 0;
            }
        }
    }

    private Task OnPartitionClosingAsync(EventProcessorClient processor, PartitionClosingEventArgs args)
    {
        Logger.ClosingProcessor(eventHubName: processor.EventHubName,
                                consumerGroup: processor.ConsumerGroup,
                                partitionId: args.PartitionId,
                                reason: args.Reason);
        return Task.CompletedTask;
    }

    private Task OnPartitionInitializingAsync(EventProcessorClient processor, PartitionInitializingEventArgs args)
    {
        Logger.OpeningProcessor(eventHubName: processor.EventHubName,
                                consumerGroup: processor.ConsumerGroup,
                                partitionId: args.PartitionId,
                                position: args.DefaultStartingPosition);
        return Task.CompletedTask;
    }

    private Task OnProcessErrorAsync(EventProcessorClient processor, ProcessErrorEventArgs args)
    {
        // TODO: decide on whether to restart (Stop() then Start()) or terminate (recreate processor) processing
        Logger.ProcessingError(eventHubName: processor.EventHubName,
                               consumerGroup: processor.ConsumerGroup,
                               partitionId: args.PartitionId,
                               operation: args.Operation,
                               ex: args.Exception);
        return Task.CompletedTask;
    }

    internal static bool CanCheckpoint(bool successful, UnhandledConsumerErrorBehaviour? behaviour)
    {
        /*
         * We should only checkpoint if successful or we are discarding or dead-lettering.
         * Otherwise the consumer should be allowed to handle the event again.
         * */
        return successful
               || behaviour == UnhandledConsumerErrorBehaviour.Deadletter
               || behaviour == UnhandledConsumerErrorBehaviour.Discard;
    }
}
