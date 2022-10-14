using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;

namespace Tingle.EventBus.Transports.Azure.QueueStorage;

/// <summary>
/// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using Azure Queue Storage.
/// </summary>
[TransportName(TransportNames.AzureQueueStorage)]
public class AzureQueueStorageTransport : EventBusTransportBase<AzureQueueStorageTransportOptions>, IDisposable
{
    private readonly Dictionary<(Type, bool), QueueClient> queueClientsCache = new();
    private readonly SemaphoreSlim queueClientsCacheLock = new(1, 1); // only one at a time.
    private readonly CancellationTokenSource stoppingCts = new();
    private readonly List<Task> receiverTasks = new();
    private readonly QueueServiceClient serviceClient;
    private bool disposedValue;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="transportOptionsAccessor"></param>
    /// <param name="loggerFactory"></param>
    public AzureQueueStorageTransport(IServiceScopeFactory serviceScopeFactory,
                                      IOptions<EventBusOptions> busOptionsAccessor,
                                      IOptions<AzureQueueStorageTransportOptions> transportOptionsAccessor,
                                      ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
    {
        var cred = TransportOptions.Credentials.CurrentValue;
        serviceClient = cred is AzureQueueStorageTransportCredentials aqstc
                ? new QueueServiceClient(serviceUri: aqstc.ServiceUrl, credential: aqstc.TokenCredential)
                : new QueueServiceClient(connectionString: (string)cred);
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
                var t = ReceiveAsync(reg: reg, ecr: ecr, cancellationToken: stoppingCts.Token);
                receiverTasks.Add(t);
            }
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        // Stop called without start or there was no consumers registered
        if (receiverTasks.Count == 0) return;

        try
        {
            // Signal cancellation to the executing methods/tasks
            stoppingCts.Cancel();
        }
        finally
        {
            // Wait until the tasks complete or the stop token triggers
            var tasks = receiverTasks.Concat(new[] { Task.Delay(Timeout.Infinite, cancellationToken), });
            await Task.WhenAny(tasks);
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
                                        cancellationToken: cancellationToken);


        // if scheduled for later, calculate the visibility timeout
        var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

        // if expiry is set, calculate the TTL
        var ttl = @event.Expires - DateTimeOffset.UtcNow;

        // get the queue client and send the message
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
        Logger.SendingMessage(eventBusId: @event.Id, queueName: queueClient.Name, scheduled: scheduled);
        var response = await queueClient.SendMessageAsync(messageText: body.ToString(),
                                                          visibilityTimeout: visibilityTimeout,
                                                          timeToLive: ttl,
                                                          cancellationToken: cancellationToken);

        // return the sequence number; both MessageId and PopReceipt are needed to update or delete
        return scheduled != null
                ? new ScheduledResult(id: (AzureQueueStorageSchedulingId)response.Value, scheduled: scheduled.Value)
                : null;
    }

    /// <inheritdoc/>
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                    EventRegistration registration,
                                                                                    DateTimeOffset? scheduled = null,
                                                                                    CancellationToken cancellationToken = default)
    {
        // log warning when doing batch
        Logger.BatchingNotSupported();

        using var scope = CreateScope();

        // work on each event
        var sequenceNumbers = new List<string>();
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
        foreach (var @event in events)
        {
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken);
            // if scheduled for later, calculate the visibility timeout
            var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

            // if expiry is set, calculate the TTL
            var ttl = @event.Expires - DateTimeOffset.UtcNow;

            // send the message
            Logger.SendingMessage(eventBusId: @event.Id, queueName: queueClient.Name, scheduled: scheduled);
            var response = await queueClient.SendMessageAsync(messageText: body.ToString(),
                                                              visibilityTimeout: visibilityTimeout,
                                                              timeToLive: ttl,
                                                              cancellationToken: cancellationToken);
            // collect the sequence number
            sequenceNumbers.Add((AzureQueueStorageSchedulingId)response.Value);
        }

        // return the sequence number
        return scheduled != null ? sequenceNumbers.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList() : null;
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

        if (!AzureQueueStorageSchedulingId.TryParse(id, out var sid))
        {
            throw new ArgumentException($"'{nameof(id)}' is malformed or invalid", nameof(id));
        }

        // get the queue client and cancel the message accordingly
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
        Logger.CancelingMessage(messageId: sid.MessageId, popReceipt: sid.PopReceipt, queueName: queueClient.Name);
        await queueClient.DeleteMessageAsync(messageId: sid.MessageId,
                                             popReceipt: sid.PopReceipt,
                                             cancellationToken: cancellationToken);
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

        // log warning when doing batch
        Logger.BatchingNotSupported();

        var sids = ids.Select(i =>
        {
            if (!AzureQueueStorageSchedulingId.TryParse(i, out var sid))
            {
                throw new ArgumentException($"'{nameof(i)}' is malformed or invalid", nameof(i));
            }
            return sid;
        }).ToList();

        // get the queue client and cancel the messages accordingly
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
        foreach (var id in sids)
        {
            Logger.CancelingMessage(messageId: id.MessageId, popReceipt: id.PopReceipt, queueName: queueClient.Name);
            await queueClient.DeleteMessageAsync(messageId: id.MessageId,
                                                 popReceipt: id.PopReceipt,
                                                 cancellationToken: cancellationToken);
        }
    }

    private async Task<QueueClient> GetQueueClientAsync(EventRegistration reg, bool deadletter, CancellationToken cancellationToken)
    {
        await queueClientsCacheLock.WaitAsync(cancellationToken);

        try
        {
            if (!queueClientsCache.TryGetValue((reg.EventType, deadletter), out var queueClient))
            {
                var name = reg.EventName!;
                if (deadletter) name += TransportOptions.DeadLetterSuffix;

                // create the queue client options
                var qco = new QueueClientOptions
                {
                    // Using base64 encoding allows for complex data like JSON
                    // to be embedded in the generated XML request body
                    // https://github.com/Azure-Samples/storage-queue-dotnet-getting-started/issues/4
                    MessageEncoding = QueueMessageEncoding.Base64,
                };

                // create the queue client
                // queueUri has the format "https://{account_name}.queue.core.windows.net/{queue_name}" which can be made using "{serviceClient.Uri}/{queue_name}"
                var cred = TransportOptions.Credentials.CurrentValue;
                queueClient = cred is AzureQueueStorageTransportCredentials aqstc
                    ? new QueueClient(queueUri: new Uri($"{serviceClient.Uri}/{name}"), credential: aqstc.TokenCredential, options: qco)
                    : new QueueClient(connectionString: (string)cred, queueName: name, options: qco);

                // if entity creation is enabled, ensure queue is created
                if (TransportOptions.EnableEntityCreation)
                {
                    // ensure queue is created if it does not exist
                    Logger.EnsuringQueue(queueName: name);
                    await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    Logger.EntityCreationDisabled();
                }

                queueClientsCache[(reg.EventType, deadletter)] = queueClient;
            }

            return queueClient;
        }
        finally
        {
            queueClientsCacheLock.Release();
        }
    }

    private async Task ReceiveAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags) ?? throw new InvalidOperationException("Methods should be null");
        var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);

        var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await queueClient.ReceiveMessagesAsync(cancellationToken);
                var messages = response.Value;

                // if the response is empty, introduce a delay
                if (messages.Length == 0)
                {
                    var delay = TransportOptions.EmptyResultsDelay;
                    Logger.NoMessages(queueName: queueClient.Name, delay: delay);
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    Logger.ReceivedMessages(messagesCount: messages.Length, queueName: queueClient.Name);
                    using var scope = CreateScope(); // shared
                    foreach (var message in messages)
                    {
                        await (Task)method.Invoke(this, new object[] { reg, ecr, queueClient, message, scope, cancellationToken, })!;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore
                // Thrown from inside Task.Delay(...) if cancellation token is canceled
            }
            catch (OperationCanceledException)
            {
                // Ignore
                // Thrown from calls to cancellationToken.ThrowIfCancellationRequested(...)
            }
        }
    }

    private async Task OnMessageReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                 EventConsumerRegistration ecr,
                                                                 QueueClient queueClient,
                                                                 QueueMessage message,
                                                                 IServiceScope scope,
                                                                 CancellationToken cancellationToken)
        where TEvent : class
        where TConsumer : IEventConsumer<TEvent>
    {
        var messageId = message.MessageId;
        using var log_scope = BeginLoggingScopeForConsume(id: messageId,
                                                          correlationId: null,
                                                          extras: new Dictionary<string, string?>
                                                          {
                                                              ["PopReceipt"] = message.PopReceipt,
                                                          });

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer); // no way to get parentId at this point
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
        activity?.AddTag(ActivityTagNames.MessagingSystem, Name);
        activity?.AddTag(ActivityTagNames.MessagingDestination, queueClient.Name);
        activity?.AddTag(ActivityTagNames.MessagingDestinationKind, "queue");

        Logger.ProcessingMessage(messageId: messageId, queueName: queueClient.Name);
        var context = await DeserializeAsync<TEvent>(scope: scope,
                                                     body: message.Body,
                                                     contentType: null,
                                                     registration: reg,
                                                     identifier: (AzureQueueStorageSchedulingId)message,
                                                     raw: message,
                                                     cancellationToken: cancellationToken);

        Logger.ReceivedMessage(messageId: messageId, eventBusId: context.Id, queueName: queueClient.Name);

        // if the event contains the parent activity id, set it
        if (context.Headers.TryGetValue(HeaderNames.ActivityId, out var parentActivityId))
        {
            activity?.SetParentId(parentId: parentActivityId);
        }

        var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(ecr: ecr,
                                                                    @event: context,
                                                                    scope: scope,
                                                                    cancellationToken: cancellationToken);

        if (!successful && ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
        {
            // get the client for the dead letter queue and send the message there
            var dlqClient = await GetQueueClientAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
            await dlqClient.SendMessageAsync(message.MessageText, cancellationToken);
        }

        // whether or not successful, always delete the message from the current queue
        Logger.DeletingMessage(messageId: messageId, queueName: queueClient.Name);
        await queueClient.DeleteMessageAsync(messageId: messageId,
                                             popReceipt: message.PopReceipt,
                                             cancellationToken: cancellationToken);
    }

    ///
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                stoppingCts.Cancel();
            }

            disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
