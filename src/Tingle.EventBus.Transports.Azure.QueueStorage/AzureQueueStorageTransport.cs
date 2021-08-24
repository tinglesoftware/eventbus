using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Azure.QueueStorage
{
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
            var cred = TransportOptions.Credentials!.Value!;
            serviceClient = cred is AzureQueueStorageTransportCredentials aqstc
                    ? new QueueServiceClient(serviceUri: aqstc.ServiceUrl, credential: aqstc.TokenCredential)
                    : new QueueServiceClient(connectionString: (string)cred);
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                          CancellationToken cancellationToken = default)
        {
            /* GetStatisticsAsync fails to work as expected, instead we use GetPropertiesAsync */
            await serviceClient.GetPropertiesAsync(cancellationToken);
            return true;
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
        public override async Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                          EventRegistration registration,
                                                                          DateTimeOffset? scheduled = null,
                                                                          CancellationToken cancellationToken = default)
        {
            using var scope = CreateScope();
            using var ms = new MemoryStream();
            await SerializeAsync(scope: scope,
                                 body: ms,
                                 @event: @event,
                                 registration: registration,
                                 cancellationToken: cancellationToken);


            // if scheduled for later, calculate the visibility timeout
            var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

            // if expiry is set, calculate the ttl
            var ttl = @event.Expires - DateTimeOffset.UtcNow;

            // get the queue client and send the message
            var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
            var message = (await BinaryData.FromStreamAsync(ms, cancellationToken)).ToString();
            Logger.LogInformation("Sending {Id} to '{QueueName}'. Scheduled: {Scheduled}", @event.Id, queueClient.Name, scheduled);
            var response = await queueClient.SendMessageAsync(messageText: message,
                                                              visibilityTimeout: visibilityTimeout,
                                                              timeToLive: ttl,
                                                              cancellationToken: cancellationToken);

            // return the sequence number; both MessageId and PopReceipt are needed to update or delete
            return scheduled != null
                    ? new ScheduledResult(id: (AzureQueueStorageSchedulingId)response.Value, scheduled: scheduled.Value)
                    : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                 EventRegistration registration,
                                                                                 DateTimeOffset? scheduled = null,
                                                                                 CancellationToken cancellationToken = default)
        {
            // log warning when doing batch
            Logger.LogWarning("Azure Queue Storage does not support batching. The events will be looped through one by one");

            using var scope = CreateScope();

            // work on each event
            var sequenceNumbers = new List<string>();
            var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                await SerializeAsync(scope: scope,
                                     body: ms,
                                     @event: @event,
                                     registration: registration,
                                     cancellationToken: cancellationToken);
                // if scheduled for later, calculate the visibility timeout
                var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

                // if expiry is set, calculate the ttl
                var ttl = @event.Expires - DateTimeOffset.UtcNow;

                // send the message
                var message = (await BinaryData.FromStreamAsync(ms, cancellationToken)).ToString();
                Logger.LogInformation("Sending {Id} to '{QueueName}'. Scheduled: {Scheduled}", @event.Id, queueClient.Name, scheduled);
                var response = await queueClient.SendMessageAsync(messageText: message,
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

            var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);
            if (AzureQueueStorageSchedulingId.TryParse(id, out var sid))
            {
                var messageId = sid.MessageId;
                var popReceipt = sid.PopReceipt;
                Logger.LogInformation("Cancelling '{MessageId}|{PopReceipt}' on '{QueueName}'", messageId, popReceipt, queueClient.Name);
                await queueClient.DeleteMessageAsync(messageId: messageId,
                                                     popReceipt: popReceipt,
                                                     cancellationToken: cancellationToken);
            }
            else
            {
                Logger.LogWarning("The provided id '{Id}' does not match the expected format.", id);
            }
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
            Logger.LogWarning("Azure Queue Storage does not support batching. The events will be canceled one by one");

            var queueClient = await GetQueueClientAsync(reg: registration, deadletter: false, cancellationToken: cancellationToken);

            foreach (var id in ids)
            {
                if (AzureQueueStorageSchedulingId.TryParse(id, out var sid))
                {
                    var messageId = sid.MessageId;
                    var popReceipt = sid.PopReceipt;
                    Logger.LogInformation("Cancelling '{MessageId}|{PopReceipt}' on '{QueueName}'", messageId, popReceipt, queueClient.Name);
                    await queueClient.DeleteMessageAsync(messageId: messageId,
                                                         popReceipt: popReceipt,
                                                         cancellationToken: cancellationToken);
                }
                else
                {
                    Logger.LogWarning("The provided id '{Id}' does not match the expected format.", id);
                }
            }
        }

        private async Task<QueueClient> GetQueueClientAsync(EventRegistration reg, bool deadletter, CancellationToken cancellationToken)
        {
            await queueClientsCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!queueClientsCache.TryGetValue((reg.EventType, deadletter), out var queueClient))
                {
                    var name = reg.EventName;
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
                    var cred = TransportOptions.Credentials!.Value!;
                    queueClient = cred is AzureQueueStorageTransportCredentials aqstc
                        ? new QueueClient(queueUri: new Uri($"{serviceClient.Uri}/{name}"), credential: aqstc.TokenCredential, options: qco)
                        : new QueueClient(connectionString: (string)cred, queueName: name, options: qco);

                    // if entity creation is enabled, ensure queue is created
                    if (TransportOptions.EnableEntityCreation)
                    {
                        // ensure queue is created if it does not exist
                        Logger.LogInformation("Ensuring queue '{QueueName}' exists", name);
                        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                    }
                    else
                    {
                        Logger.LogTrace("Entity creation is diabled. Queue creation skipped");
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
            var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
            var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);

            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await queueClient.ReceiveMessagesAsync();
                    var messages = response.Value;

                    // if the response is empty, introduce a delay
                    if (messages.Length == 0)
                    {
                        var delay = TransportOptions.EmptyResultsDelay;
                        Logger.LogTrace("No messages on '{QueueName}', delaying check for {Delay}", queueClient.Name, delay);
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {
                        Logger.LogDebug("Received {MessageCount} messages on '{QueueName}'", messages.Length, queueClient.Name);
                        using var scope = CreateScope(); // shared
                        foreach (var message in messages)
                        {
                            await (Task)method.Invoke(this, new object[] { reg, ecr, queueClient, message, scope, cancellationToken, });
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

            Logger.LogDebug("Processing '{MessageId}' from '{QueueName}'", messageId, queueClient.Name);
            var context = await DeserializeAsync<TEvent>(scope: scope,
                                                         body: message.Body,
                                                         contentType: null,
                                                         registration: reg,
                                                         identifier: (AzureQueueStorageSchedulingId)message,
                                                         cancellationToken: cancellationToken);

            Logger.LogInformation("Received message: '{MessageId}' containing Event '{Id}' from '{QueueName}'",
                                  messageId,
                                  context.Id,
                                  queueClient.Name);

            // if the event contains the parent activity id, set it
            if (context.Headers.TryGetValue(HeaderNames.ActivityId, out var parentActivityId))
            {
                activity?.SetParentId(parentId: parentActivityId!.ToString());
            }

            var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(ecr: ecr,
                                                                        @event: context,
                                                                        scope: scope,
                                                                        cancellationToken: cancellationToken);

            if (!successful && ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
            {
                // get the client for the dead letter queue and send the mesage there
                var dlqClient = await GetQueueClientAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                await dlqClient.SendMessageAsync(message.MessageText, cancellationToken);
            }

            // whether or not successful, always delete the message from the current queue
            Logger.LogTrace("Deleting '{MessageId}' on '{QueueName}'", messageId, queueClient.Name);
            await queueClient.DeleteMessageAsync(messageId: messageId,
                                                 popReceipt: message.PopReceipt,
                                                 cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            stoppingCts.Cancel();
        }
    }
}
