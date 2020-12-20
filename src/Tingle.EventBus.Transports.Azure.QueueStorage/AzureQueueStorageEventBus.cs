﻿using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.Azure.QueueStorage
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using Azure Queue Storage.
    /// </summary>
    public class AzureQueueStorageEventBus : EventBusBase<AzureQueueStorageOptions>
    {
        private const string SequenceNumberSeparator = "|";

        private readonly Dictionary<(Type, bool), QueueClient> queueClientsCache = new Dictionary<(Type, bool), QueueClient>();
        private readonly SemaphoreSlim queueClientsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
        private readonly CancellationTokenSource receiveCancellationTokenSource = new CancellationTokenSource();
        private readonly QueueServiceClient serviceClient;
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureQueueStorageEventBus(IHostEnvironment environment,
                                         IServiceScopeFactory serviceScopeFactory,
                                         IOptions<EventBusOptions> busOptionsAccessor,
                                         IOptions<AzureQueueStorageOptions> transportOptionsAccessor,
                                         ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            serviceClient = new QueueServiceClient(TransportOptions.ConnectionString);
            logger = loggerFactory?.CreateLogger<AzureQueueStorageEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            await serviceClient.GetStatisticsAsync(cancellationToken);
            return true;
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = BusOptions.GetConsumerRegistrations();
            foreach (var reg in registrations)
            {
                _ = ReceiveAsync(reg);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            receiveCancellationTokenSource.Cancel();
            // TODO: figure out a way to wait for notification of termination in all receivers
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   serializerType: reg.EventSerializerType,
                                                   cancellationToken: cancellationToken);


            // if scheduled for later, calculate the visibility timeout
            var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

            // if expiry is set, calculate the ttl
            var ttl = @event.Expires - DateTimeOffset.UtcNow;

            // get the queue client and send the message
            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            var message = Encoding.UTF8.GetString(ms.ToArray());
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
            logger.LogWarning("Azure Queue Storage does not support batching. The events will be looped through one by one");

            // work on each event
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var sequenceNumbers = new List<string>();
            var queueClient = await GetQueueClientAsync(reg: reg, deadletter: false, cancellationToken: cancellationToken);
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       serializerType: reg.EventSerializerType,
                                                       cancellationToken: cancellationToken);
                // if scheduled for later, calculate the visibility timeout
                var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

                // if expiry is set, calculate the ttl
                var ttl = @event.Expires - DateTimeOffset.UtcNow;

                // send the message
                var message = Encoding.UTF8.GetString(ms.ToArray());
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

                    // ensure queue is created
                    await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

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
                    await Task.Delay(TransportOptions.EmptyResultsDelay, cancellationToken);
                }
                else
                {
                    foreach (var message in messages)
                    {
                        await (Task)method.Invoke(this, new object[] { reg, queueClient, message, cancellationToken, });
                    }
                }
            }
        }

        private async Task OnMessageReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg, QueueClient queueClient, QueueMessage message, CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            using var log_scope = logger.BeginScope(new Dictionary<string, string>
            {
                ["MesageId"] = message.MessageId,
                ["PopReceipt"] = message.PopReceipt,
                ["CorrelationId"] = null,
                ["SequenceNumber"] = null,
            });

            try
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(message.MessageText));
                var contentType = new ContentType("*/*");
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             serializerType: reg.EventSerializerType,
                                                             cancellationToken: cancellationToken);
                await PushToConsumerAsync<TEvent, TConsumer>(context, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing failed. Moving to deadletter.");

                // get the client for the dead letter queue and send the mesage there
                var dlqClient = await GetQueueClientAsync(reg: reg, deadletter: true, cancellationToken: cancellationToken);
                await dlqClient.SendMessageAsync(message.MessageText, cancellationToken);
            }

            // always delete the message from the current queue
            await queueClient.DeleteMessageAsync(messageId: message.MessageId,
                                                 popReceipt: message.PopReceipt,
                                                 cancellationToken: cancellationToken);
        }
    }
}
