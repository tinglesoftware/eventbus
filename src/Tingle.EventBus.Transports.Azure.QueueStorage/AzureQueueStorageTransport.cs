using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
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
    public class AzureQueueStorageTransport : EventBusTransportBase<AzureQueueStorageTransportOptions>
    {
        private const string SequenceNumberSeparator = "|";

        private readonly Dictionary<(Type, bool), QueueClient> queueClientsCache = new Dictionary<(Type, bool), QueueClient>();
        private readonly SemaphoreSlim queueClientsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly CancellationTokenSource receiveCancellationTokenSource = new CancellationTokenSource();
        private readonly QueueServiceClient serviceClient;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureQueueStorageTransport(IHostEnvironment environment,
                                          IServiceScopeFactory serviceScopeFactory,
                                          IOptions<EventBusOptions> busOptionsAccessor,
                                          IOptions<AzureQueueStorageTransportOptions> transportOptionsAccessor,
                                          ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            serviceClient = new QueueServiceClient(TransportOptions.ConnectionString);
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
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = GetConsumerRegistrations();
            Logger.StartingTransport(registrations.Count);
            foreach (var reg in registrations)
            {
                _ = ReceiveAsync(reg);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.StoppingTransport();
            receiveCancellationTokenSource.Cancel();
            // TODO: figure out a way to wait for notification of termination in all receivers
            return Task.CompletedTask;
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


            // if scheduled for later, calculate the visibility timeout
            var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

            // if expiry is set, calculate the ttl
            var ttl = @event.Expires - DateTimeOffset.UtcNow;

            // get the queue client and send the message
            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            var message = Encoding.UTF8.GetString(ms.ToArray());
            Logger.LogInformation("Sending {Id} to '{QueueName}'. Scheduled: {Scheduled}", @event.Id, queueClient.Name, scheduled);
            var response = await queueClient.SendMessageAsync(messageText: message,
                                                              visibilityTimeout: visibilityTimeout,
                                                              timeToLive: ttl,
                                                              cancellationToken: cancellationToken);

            // return the sequence number; both MessageId and PopReceipt are needed to update or delete
            return scheduled != null ? string.Join(SequenceNumberSeparator, response.Value.MessageId, response.Value.PopReceipt) : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        {
            // log warning when doing batch
            Logger.LogWarning("Azure Queue Storage does not support batching. The events will be looped through one by one");

            using var scope = CreateScope();

            // work on each event
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var sequenceNumbers = new List<string>();
            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       registration: reg,
                                                       scope: scope,
                                                       cancellationToken: cancellationToken);
                // if scheduled for later, calculate the visibility timeout
                var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

                // if expiry is set, calculate the ttl
                var ttl = @event.Expires - DateTimeOffset.UtcNow;

                // send the message
                var message = Encoding.UTF8.GetString(ms.ToArray());
                Logger.LogInformation("Sending {Id} to '{QueueName}'. Scheduled: {Scheduled}", @event.Id, queueClient.Name, scheduled);
                var response = await queueClient.SendMessageAsync(messageText: message,
                                                                  visibilityTimeout: visibilityTimeout,
                                                                  timeToLive: ttl,
                                                                  cancellationToken: cancellationToken);
                // collect the sequence number
                sequenceNumbers.Add(string.Join(SequenceNumberSeparator, response.Value.MessageId, response.Value.PopReceipt));
            }

            // return the sequence number
            return scheduled != null ? sequenceNumbers : null;
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace", nameof(id));
            }

            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            var parts = id.Split(SequenceNumberSeparator);
            string messageId = parts[0], popReceipt = parts[1];
            Logger.LogInformation("Cancelling '{MessageId}|{PopReceipt}' on '{QueueName}'", messageId, popReceipt, queueClient.Name);
            await queueClient.DeleteMessageAsync(messageId: messageId,
                                                 popReceipt: popReceipt,
                                                 cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
        {
            if (ids is null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var splits = ids.Select(i =>
            {
                var parts = i.Split(SequenceNumberSeparator);
                return (messageId: parts[0], popReceipt: parts[1]);
            }).ToList();

            // log warning when doing batch
            Logger.LogWarning("Azure Queue Storage does not support batching. The events will be canceled one by one");

            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            foreach (var (messageId, popReceipt) in splits)
            {
                Logger.LogInformation("Cancelling '{MessageId}|{PopReceipt}' on '{QueueName}'", messageId, popReceipt, queueClient.Name);
                await queueClient.DeleteMessageAsync(messageId: messageId,
                                                     popReceipt: popReceipt,
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
                    var name = reg.EventName;
                    if (deadletter) name += "-deadletter";

                    // create the queue client
                    queueClient = new QueueClient(connectionString: TransportOptions.ConnectionString,
                                                  queueName: name,
                                                  options: new QueueClientOptions
                                                  {
                                                      // Using base64 encoding allows for complex data like JSON
                                                      // to be embedded in the generated XML request body
                                                      // https://github.com/Azure-Samples/storage-queue-dotnet-getting-started/issues/4
                                                      MessageEncoding = QueueMessageEncoding.Base64,
                                                  });

                    // if entity creation is enabled, ensure queue is created
                    if (!TransportOptions.EnableEntityCreation)
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

        private async Task ReceiveAsync(ConsumerRegistration reg)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var mt = GetType().GetMethod(nameof(OnMessageReceivedAsync), flags);
            var method = mt.MakeGenericMethod(reg.EventType, reg.ConsumerType);
            var cancellationToken = receiveCancellationTokenSource.Token;

            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
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
                        await (Task)method.Invoke(this, new object[] { reg, queueClient, message, scope, cancellationToken, });
                    }
                }
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg,
                                                                     QueueClient queueClient,
                                                                     QueueMessage message,
                                                                     IServiceScope scope,
                                                                     CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            var messageId = message.MessageId;
            using var log_scope = Logger.BeginScopeForConsume(id: messageId,
                                                              correlationId: null,
                                                              extras: new Dictionary<string, string>
                                                              {
                                                                  ["PopReceipt"] = message.PopReceipt,
                                                              });

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer); // no way to get parentId at this point
            activity?.AddTag(ActivityTags.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTags.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTags.MessagingSystem, Name);
            activity?.AddTag(ActivityTags.MessagingDestination, queueClient.Name);
            activity?.AddTag(ActivityTags.MessagingDestinationKind, "queue");

            try
            {
                Logger.LogDebug("Processing '{MessageId}|{PopReceipt}'", messageId, message.PopReceipt);
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(message.MessageText));
                var contentType = new ContentType("*/*");
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: reg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);

                Logger.LogInformation("Received message: '{MessageId}|{PopReceipt}' containing Event '{Id}'",
                                      messageId,
                                      message.PopReceipt,
                                      context.Id);

                // if the event contains the parent activity id, set it
                if (context.Headers.TryGetValue(HeaderNames.ActivityId, out var parentActivityId))
                {
                    activity?.SetParentId(parentId: parentActivityId.ToString());
                }

                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event processing failed. Moving to deadletter.");

                // get the client for the dead letter queue and send the mesage there
                var dlqClient = await GetQueueClientAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                await dlqClient.SendMessageAsync(message.MessageText, cancellationToken);
            }

            // always delete the message from the current queue
            Logger.LogTrace("Deleting '{MessageId}|{PopReceipt}' on '{QueueName}'",
                            messageId,
                            message.PopReceipt,
                            queueClient.Name);
            await queueClient.DeleteMessageAsync(messageId: messageId,
                                                 popReceipt: message.PopReceipt,
                                                 cancellationToken: cancellationToken);
        }
    }
}
