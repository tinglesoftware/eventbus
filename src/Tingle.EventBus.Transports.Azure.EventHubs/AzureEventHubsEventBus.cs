using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.Azure.EventHubs
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using Azure Event Hubs.
    /// </summary>
    public class AzureEventHubsEventBus : EventBusBase<AzureEventHubsOptions>
    {
        private readonly Dictionary<(Type, bool), EventHubProducerClient> producersCache = new Dictionary<(Type, bool), EventHubProducerClient>();
        private readonly SemaphoreSlim producersCacheLock = new SemaphoreSlim(1, 1); // only one at a time.
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
        public override Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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

        /// <inheritdoc/>
        public override async Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                logger.LogWarning("Azure EventHubs does not support delay or scheduled publish");
            }

            var reg = BusOptions.GetRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   serializerType: reg.EventSerializerType,
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
        public override async Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
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

            var messages = new List<EventData>();
            var reg = BusOptions.GetRegistration<TEvent>();
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       serializerType: reg.EventSerializerType,
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

        private async Task<EventHubProducerClient> GetProducerAsync(EventConsumerRegistration reg, bool deadletter, CancellationToken cancellationToken)
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
                    // TODO: consider optionaly allowing for credentials to perform ARM operations?

                    producersCache[(reg.EventType, deadletter)] = producer;
                }

                return producer;
            }
            finally
            {
                producersCacheLock.Release();
            }
        }

    }
}
