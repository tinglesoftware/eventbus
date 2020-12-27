using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Kafka
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using Kafka.
    /// </summary>
    public class KafkaEventBus : EventBusBase<KafkaOptions>
    {
        // the timeout used for non-async operations
        private static readonly TimeSpan StandardTimeout = TimeSpan.FromSeconds(30);

        private readonly IProducer<string, byte[]> producer; // producer instance is thread safe thus can be shared, and across topics
        private readonly IConsumer<string, byte[]> consumer; // consumer instance is thread safe thus can be shared, and across topics
        private readonly CancellationTokenSource receiveCancellationTokenSource = new CancellationTokenSource();
        private readonly IAdminClient adminClient;
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public KafkaEventBus(IHostEnvironment environment,
                             IServiceScopeFactory serviceScopeFactory,
                             IOptions<EventBusOptions> busOptionsAccessor,
                             IOptions<KafkaOptions> transportOptionsAccessor,
                             ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            // Should be setup the logger?
            adminClient = new AdminClientBuilder(TransportOptions.AdminConfig).Build();

            // create the shared producer instance
            var pconfig = new ProducerConfig(TransportOptions.AdminConfig);
            producer = new ProducerBuilder<string, byte[]>(pconfig)
                            //.SetValueSerializer((ISerializer<byte[]>)null)
                            .Build();

            // create the shared consumer instance
            var c_config = new ConsumerConfig(TransportOptions.AdminConfig)
            {
                GroupId = BusOptions.GetApplicationName(environment),
                //EnableAutoCommit = false,
                //StatisticsIntervalMs = 5000,
                //SessionTimeoutMs = 6000,
                //AutoOffsetReset = AutoOffsetReset.Earliest,
                EnablePartitionEof = true,
            };
            consumer = new ConsumerBuilder<string, byte[]>(c_config)
                            //.SetValueSerializer((ISerializer<byte[]>)null)
                            .Build();

            logger = loggerFactory?.CreateLogger<KafkaEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public override Task<bool> CheckHealthAsync(EventBusHealthCheckExtras extras,
                                                    CancellationToken cancellationToken = default)
        {
            adminClient.GetMetadata(StandardTimeout);
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        protected override Task StartBusAsync(CancellationToken cancellationToken)
        {
            var registrations = BusOptions.GetConsumerRegistrations();
            logger.StartingBusReceivers(registrations.Count);
            var topics = registrations.Select(r => r.EventName);
            consumer.Subscribe(topics);
            _ = ProcessAsync();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override Task StopBusAsync(CancellationToken cancellationToken)
        {
            // cancel receivers
            logger.StoppingBusReceivers();
            receiveCancellationTokenSource.Cancel();

            // ensure all outstanding produce requests are processed
            producer.Flush(cancellationToken);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected async override Task<string> PublishOnBusAsync<TEvent>(EventContext<TEvent> @event,
                                                                        DateTimeOffset? scheduled = null,
                                                                        CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                logger.LogWarning("Kafka does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            var contentType = await SerializeAsync(body: ms,
                                                   @event: @event,
                                                   registration: reg,
                                                   scope: scope,
                                                   cancellationToken: cancellationToken);

            // prepare the message
            var message = new Message<string, byte[]>();
            message.Headers.Add("Content-Type", contentType.ToString());
            message.Headers.Add(nameof(@event.RequestId), @event.RequestId);
            message.Headers.Add(nameof(@event.CorrelationId), @event.CorrelationId);
            message.Headers.Add(nameof(@event.ConversationId), @event.ConversationId);
            message.Headers.Add(nameof(@event.InitiatorId), @event.InitiatorId);
            message.Key = @event.EventId;
            message.Value = ms.ToArray();

            // send the event
            var topic = reg.EventName;
            var result = await producer.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken);
            // Should we check persistance status?

            // return the sequence number
            return scheduled != null ? result.Offset.Value.ToString() : null;
        }

        /// <inheritdoc/>
        protected async override Task<IList<string>> PublishOnBusAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                               DateTimeOffset? scheduled = null,
                                                                               CancellationToken cancellationToken = default)
        {
            // log warning when doing batch
            logger.LogWarning("Kafka does not support batching. The events will be looped through one by one");

            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                logger.LogWarning("Kafka does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var sequenceNumbers = new List<string>();

            // work on each event
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                var contentType = await SerializeAsync(body: ms,
                                                       @event: @event,
                                                       registration: reg,
                                                       scope: scope,
                                                       cancellationToken: cancellationToken);

                // prepare the message
                var message = new Message<string, byte[]>();
                message.Headers.Add("Content-Type", contentType.ToString());
                message.Headers.Add(nameof(@event.RequestId), @event.RequestId);
                message.Headers.Add(nameof(@event.CorrelationId), @event.CorrelationId);
                message.Headers.Add(nameof(@event.ConversationId), @event.ConversationId);
                message.Headers.Add(nameof(@event.InitiatorId), @event.InitiatorId);
                message.Key = @event.EventId;
                message.Value = ms.ToArray();

                // send the event
                var topic = reg.EventName;
                var result = await producer.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken);
                // Should we check persistance status?

                // collect the sequence number
                sequenceNumbers.Add(result.Offset.Value.ToString());
            }

            // return the sequence numbers
            return scheduled != null ? sequenceNumbers : null;
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Kafka does not support canceling published events.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Kafka does not support canceling published events.");
        }

        private async Task ProcessAsync()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var mt = GetType().GetMethod(nameof(OnEventReceivedAsync), flags);
            var cancellationToken = receiveCancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = consumer.Consume(cancellationToken);
                if (result.IsPartitionEOF)
                {
                    logger.LogTrace("Reached end of topic {Topic}, Partition:{Partition}, Offset:{Offset}.",
                                    result.Topic,
                                    result.Partition,
                                    result.Offset);
                    continue;
                }

                logger.LogDebug("Received message at {TopicPartitionOffset}", result.TopicPartitionOffset);

                // get the registration for topic
                var topic = result.Topic;
                var reg = BusOptions.GetConsumerRegistrations().Single(r => r.EventName == topic);

                // form the generic method
                var method = mt.MakeGenericMethod(reg.EventType, reg.ConsumerType);
                await (Task)method.Invoke(this, new object[] { reg, result.Message, cancellationToken, });


                //// update the checkpoint store so that the app receives only new events the next time it's run
                //await args.UpdateCheckpointAsync(args.CancellationToken);

                // if configured to checkpoint at intervals, respect it
                if ((result.Offset % TransportOptions.CheckpointInterval) == 0)
                {
                    // The Commit method sends a "commit offsets" request to the Kafka
                    // cluster and synchronously waits for the response. This is very
                    // slow compared to the rate at which the consumer is capable of
                    // consuming messages. A high performance application will typically
                    // commit offsets relatively infrequently and be designed handle
                    // duplicate messages in the event of failure.
                    consumer.Commit(result);
                }
            }
        }

        private async Task OnEventReceivedAsync<TEvent, TConsumer>(ConsumerRegistration reg,
                                                                   Message<string, byte[]> message,
                                                                   CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventBusConsumer<TEvent>
        {
            message.Headers.TryGetValue("CorrelationId", out var correlationId);
            message.Headers.TryGetValue("Content-Type", out var contentType_str);

            using var log_scope = logger.BeginScope(new Dictionary<string, string>
            {
                ["MessageKey"] = message.Key,
                ["CorrelationId"] = correlationId?.ToString(),
            });

            try
            {
                logger.LogDebug("Processing '{MessageKey}", message.Key);
                using var scope = CreateScope();
                using var ms = new MemoryStream(message.Value);
                var contentType = new ContentType(contentType_str?.ToString() ?? "*/*");
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: reg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);
                logger.LogInformation("Received event: '{MessageKey}' containing Event '{EventId}'",
                                      message.Key,
                                      context.EventId);
                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing failed. Moving to deadletter.");

                // produce message on deadletter topic
                var dlt = reg.EventName += "-deadletter";
                await producer.ProduceAsync(topic: dlt, message: message, cancellationToken: cancellationToken);
            }
        }
    }
}
