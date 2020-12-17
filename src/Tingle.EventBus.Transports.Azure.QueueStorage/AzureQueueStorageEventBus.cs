using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly QueueServiceClient serviceClient;
        private readonly Dictionary<Type, QueueClient> queueClientsCache = new Dictionary<Type, QueueClient>();
        private readonly SemaphoreSlim queueClientsCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
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


            // if scheduled for later, calculate the visibility timeout
            var visibilityTimeout = scheduled - DateTimeOffset.UtcNow;

            // if expiry is set, calculate the ttl
            var ttl = @event.Expires - DateTimeOffset.UtcNow;

            // get the queue client and send the message
            var queueClient = await GetQueueClientAsync(reg, cancellationToken);
            var message = Encoding.UTF8.GetString(ms.ToArray());
            var response = await queueClient.SendMessageAsync(messageText: message,
                                                              visibilityTimeout: visibilityTimeout,
                                                              timeToLive: ttl,
                                                              cancellationToken: cancellationToken);

            // return the sequence number; both MessageId and PopReceipt are needed to update or delete
            return scheduled != null ? string.Join(SequenceNumberSeparator, response.Value.MessageId, response.Value.PopReceipt) : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            // log warning when doing batch
            logger.LogWarning("Azure Queue Storage does not support batching. The events will be looped through one by one");

            // work on each event
            var reg = BusOptions.GetRegistration<TEvent>();
            var sequenceNumbers = new List<string>();
            var queueClient = await GetQueueClientAsync(reg, cancellationToken);
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

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<QueueClient> GetQueueClientAsync(EventConsumerRegistration reg, CancellationToken cancellationToken)
        {
            await queueClientsCacheLock.WaitAsync(cancellationToken);

            try
            {
                if (!queueClientsCache.TryGetValue(reg.EventType, out var queueClient))
                {
                    var name = reg.EventName;

                    // create the queue client
                    queueClient = new QueueClient(connectionString: TransportOptions.ConnectionString, queueName: name);

                    // ensure queue is created
                    await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                    queueClientsCache[reg.EventType] = queueClient;
                };

                return queueClient;
            }
            finally
            {
                queueClientsCacheLock.Release();
            }
        }
    }
}
