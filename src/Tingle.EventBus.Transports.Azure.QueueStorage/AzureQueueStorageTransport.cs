using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports.Azure.QueueStorage;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using Azure Queue Storage.
/// </summary>
public class AzureQueueStorageTransport : EventBusTransport<AzureQueueStorageTransportOptions>, IDisposable
{
    private readonly EventBusConcurrentDictionary<(Type, bool), QueueClient> queueClientsCache = new();
    private readonly CancellationTokenSource stoppingCts = new();
    private readonly List<Task> receiverTasks = new();
    private readonly Lazy<QueueServiceClient> serviceClient;
    private bool disposedValue;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public AzureQueueStorageTransport(IServiceScopeFactory serviceScopeFactory,
                                      IOptions<EventBusOptions> busOptionsAccessor,
                                      IOptionsMonitor<AzureQueueStorageTransportOptions> optionsMonitor,
                                      ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory)
    {
        serviceClient = new Lazy<QueueServiceClient>(() =>
        {
            var cred = Options.Credentials.CurrentValue;
            return cred is AzureQueueStorageTransportCredentials aqstc
                        ? new QueueServiceClient(serviceUri: aqstc.ServiceUrl, credential: aqstc.TokenCredential)
                        : new QueueServiceClient(connectionString: (string)cred);
        });
    }

    /// <inheritdoc/>
    protected override Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var registrations = GetRegistrations();
        foreach (var reg in registrations)
        {
            foreach (var ecr in reg.Consumers.Values)
            {
                var t = ReceiveAsync(reg: reg, ecr: ecr, cancellationToken: stoppingCts.Token);
                receiverTasks.Add(t);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
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
            await Task.WhenAny(tasks).ConfigureAwait(false);
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


        // if scheduled for later, calculate the visibility timeout
        var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

        // if expiry is set, calculate the TTL
        var ttl = @event.Expires - DateTimeOffset.UtcNow;

        // get the queue client and send the message
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.SendingMessage(eventBusId: @event.Id, queueName: queueClient.Name, scheduled: scheduled);
        var response = await queueClient.SendMessageAsync(messageText: body.ToString(),
                                                          visibilityTimeout: visibilityTimeout,
                                                          timeToLive: ttl,
                                                          cancellationToken: cancellationToken).ConfigureAwait(false);

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
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var @event in events)
        {
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);
            // if scheduled for later, calculate the visibility timeout
            var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

            // if expiry is set, calculate the TTL
            var ttl = @event.Expires - DateTimeOffset.UtcNow;

            // send the message
            Logger.SendingMessage(eventBusId: @event.Id, queueName: queueClient.Name, scheduled: scheduled);
            var response = await queueClient.SendMessageAsync(messageText: body.ToString(),
                                                              visibilityTimeout: visibilityTimeout,
                                                              timeToLive: ttl,
                                                              cancellationToken: cancellationToken).ConfigureAwait(false);
            // collect the sequence number
            sequenceNumbers.Add((AzureQueueStorageSchedulingId)response.Value);
        }

        // return the sequence number
        return scheduled != null ? sequenceNumbers.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList() : null;
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

        if (!AzureQueueStorageSchedulingId.TryParse(id, out var sid))
        {
            throw new ArgumentException($"'{nameof(id)}' is malformed or invalid", nameof(id));
        }

        // get the queue client and cancel the message accordingly
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.CancelingMessage(messageId: sid.MessageId, popReceipt: sid.PopReceipt, queueName: queueClient.Name);
        await queueClient.DeleteMessageAsync(messageId: sid.MessageId,
                                             popReceipt: sid.PopReceipt,
                                             cancellationToken: cancellationToken).ConfigureAwait(false);
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
        var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var id in sids)
        {
            Logger.CancelingMessage(messageId: id.MessageId, popReceipt: id.PopReceipt, queueName: queueClient.Name);
            await queueClient.DeleteMessageAsync(messageId: id.MessageId,
                                                 popReceipt: id.PopReceipt,
                                                 cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<QueueClient> GetQueueClientAsync(EventRegistration reg, bool deadletter, CancellationToken cancellationToken)
    {
        async Task<QueueClient> creator((Type, bool) _, CancellationToken ct)
        {
            var name = reg.EventName!;
            if (deadletter) name += Options.DeadLetterSuffix;

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
            var cred = Options.Credentials.CurrentValue;
            var queueClient = cred is AzureQueueStorageTransportCredentials aqstc
                ? new QueueClient(queueUri: new Uri($"{serviceClient.Value.Uri}/{name}"), credential: aqstc.TokenCredential, options: qco)
                : new QueueClient(connectionString: (string)cred, queueName: name, options: qco);

            // if entity creation is enabled, ensure queue is created
            if (Options.EnableEntityCreation)
            {
                // ensure queue is created if it does not exist
                Logger.EnsuringQueue(queueName: name);
                await queueClient.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
            }
            else
            {
                Logger.EntityCreationDisabled();
            }

            return queueClient;
        }
        return queueClientsCache.GetOrAddAsync((reg.EventType, deadletter), creator, cancellationToken);
    }

    private async Task ReceiveAsync(EventRegistration reg, EventConsumerRegistration ecr, CancellationToken cancellationToken)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags) ?? throw new InvalidOperationException("Methods should be null");
        var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);

        var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await queueClient.ReceiveMessagesAsync(cancellationToken).ConfigureAwait(false);
                var messages = response.Value;

                // if the response is empty, introduce a delay
                if (messages.Length == 0)
                {
                    var delay = Options.EmptyResultsDelay;
                    Logger.NoMessages(queueName: queueClient.Name, delay: delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Logger.ReceivedMessages(messagesCount: messages.Length, queueName: queueClient.Name);
                    using var scope = CreateScope(); // shared
                    foreach (var message in messages)
                    {
                        await ((Task)method.Invoke(this, new object[] { reg, ecr, queueClient, message, scope, cancellationToken, })!).ConfigureAwait(false);
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
                                                     deadletter: ecr.Deadletter,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);

        Logger.ReceivedMessage(messageId: messageId, eventBusId: context.Id, queueName: queueClient.Name);

        // if the event contains the parent activity id, set it
        if (context.Headers.TryGetValue(HeaderNames.ActivityId, out var parentActivityId))
        {
            activity?.SetParentId(parentId: parentActivityId);
        }

        var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(registration: reg,
                                                                    ecr: ecr,
                                                                    @event: context,
                                                                    scope: scope,
                                                                    cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!successful && ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
        {
            // get the client for the dead letter queue and send the message there
            var dlqClient = await GetQueueClientAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            await dlqClient.SendMessageAsync(message.MessageText, cancellationToken).ConfigureAwait(false);
        }

        // whether or not successful, always delete the message from the current queue
        Logger.DeletingMessage(messageId: messageId, queueName: queueClient.Name);
        await queueClient.DeleteMessageAsync(messageId: messageId,
                                             popReceipt: message.PopReceipt,
                                             cancellationToken: cancellationToken).ConfigureAwait(false);
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
