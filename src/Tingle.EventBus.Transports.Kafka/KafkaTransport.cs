using Confluent.Kafka;
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
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Diagnostics;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Kafka
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using Kafka.
    /// </summary>
    [TransportName(TransportNames.Kafka)]
    public class KafkaTransport : EventBusTransportBase<KafkaTransportOptions>
    {
        // the timeout used for non-async operations
        private static readonly TimeSpan StandardTimeout = TimeSpan.FromSeconds(30);

        private readonly IProducer<string, byte[]> producer; // producer instance is thread safe thus can be shared, and across topics
        private readonly IConsumer<string, byte[]> consumer; // consumer instance is thread safe thus can be shared, and across topics
        private readonly CancellationTokenSource receiveCancellationTokenSource = new CancellationTokenSource();
        private readonly IAdminClient adminClient;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public KafkaTransport(IHostEnvironment environment,
                              IServiceScopeFactory serviceScopeFactory,
                              IOptions<EventBusOptions> busOptionsAccessor,
                              IOptions<KafkaTransportOptions> transportOptionsAccessor,
                              ILoggerFactory loggerFactory)
            : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
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
        }

        /// <inheritdoc/>
        public override Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                    CancellationToken cancellationToken = default)
        {
            adminClient.GetMetadata(StandardTimeout);
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var registrations = GetConsumerRegistrations();
            Logger.StartingTransport(registrations.Count);
            var topics = registrations.Select(r => r.EventName).ToList();
            // only consume if there are topics to subscribe to
            if (topics.Count > 0)
            {
                consumer.Subscribe(topics);
                _ = ProcessAsync();
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            // cancel receivers
            Logger.StoppingTransport();
            receiveCancellationTokenSource.Cancel();

            // ensure all outstanding produce requests are processed
            producer.Flush(cancellationToken);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async override Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                DateTimeOffset? scheduled = null,
                                                                CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Kafka does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            using var ms = new MemoryStream();
            await SerializeAsync(body: ms,
                                 @event: @event,
                                 registration: reg,
                                 scope: scope,
                                 cancellationToken: cancellationToken);

            // prepare the message
            var message = new Message<string, byte[]>();
            message.Headers.AddIfNotNull(AttributeNames.CorrelationId, @event.CorrelationId)
                           .AddIfNotNull(AttributeNames.ContentType, @event.ContentType.ToString())
                           .AddIfNotNull(AttributeNames.RequestId, @event.RequestId)
                           .AddIfNotNull(AttributeNames.InitiatorId, @event.InitiatorId)
                           .AddIfNotNull(AttributeNames.ActivityId, Activity.Current?.Id);
            message.Key = @event.Id;
            message.Value = ms.ToArray();

            // send the event
            var topic = reg.EventName;
            var result = await producer.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken);
            // Should we check persistence status?

            // return the sequence number
            return scheduled != null ? result.Offset.Value.ToString() : null;
        }

        /// <inheritdoc/>
        public async override Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                       DateTimeOffset? scheduled = null,
                                                                       CancellationToken cancellationToken = default)
        {
            // log warning when doing batch
            Logger.LogWarning("Kafka does not support batching. The events will be looped through one by one");

            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Kafka does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var reg = BusOptions.GetOrCreateEventRegistration<TEvent>();
            var sequenceNumbers = new List<string>();

            // work on each event
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                await SerializeAsync(body: ms,
                                     @event: @event,
                                     registration: reg,
                                     scope: scope,
                                     cancellationToken: cancellationToken);

                // prepare the message
                var message = new Message<string, byte[]>();
                message.Headers.AddIfNotNull(AttributeNames.CorrelationId, @event.CorrelationId)
                               .AddIfNotNull(AttributeNames.ContentType, @event.ContentType.ToString())
                               .AddIfNotNull(AttributeNames.RequestId, @event.RequestId)
                               .AddIfNotNull(AttributeNames.InitiatorId, @event.InitiatorId)
                               .AddIfNotNull(AttributeNames.ActivityId, Activity.Current?.Id);
                message.Key = @event.Id;
                message.Value = ms.ToArray();

                // send the event
                var topic = reg.EventName;
                var result = await producer.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken);
                // Should we check persistence status?

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
                    Logger.LogTrace("Reached end of topic {Topic}, Partition:{Partition}, Offset:{Offset}.",
                                    result.Topic,
                                    result.Partition,
                                    result.Offset);
                    continue;
                }

                Logger.LogDebug("Received message at {TopicPartitionOffset}", result.TopicPartitionOffset);

                // get the registration for topic
                var topic = result.Topic;
                var reg = GetConsumerRegistrations().Single(r => r.EventName == topic);

                // form the generic method
                var method = mt.MakeGenericMethod(reg.EventType, reg.ConsumerType);
                await (Task)method.Invoke(this, new object[] { reg, result.Message, cancellationToken, });


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
            var messageKey = message.Key;
            message.Headers.TryGetValue(AttributeNames.CorrelationId, out var correlationId);
            message.Headers.TryGetValue(AttributeNames.ContentType, out var contentType_str);
            message.Headers.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

            using var log_scope = Logger.BeginScopeForConsume(id: messageKey, correlationId: correlationId);

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);

            try
            {
                Logger.LogDebug("Processing '{MessageKey}", messageKey);
                using var scope = CreateScope();
                using var ms = new MemoryStream(message.Value);
                var contentType = contentType_str == null ? null : new ContentType(contentType_str.ToString());
                var context = await DeserializeAsync<TEvent>(body: ms,
                                                             contentType: contentType,
                                                             registration: reg,
                                                             scope: scope,
                                                             cancellationToken: cancellationToken);
                Logger.LogInformation("Received event: '{MessageKey}' containing Event '{Id}'",
                                      messageKey,
                                      context.Id);
                await ConsumeAsync<TEvent, TConsumer>(@event: context,
                                                      scope: scope,
                                                      cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Event processing failed. Moving to deadletter.");

                // produce message on deadletter topic
                var dlt = reg.EventName += TransportOptions.DeadLetterSuffix;
                await producer.ProduceAsync(topic: dlt, message: message, cancellationToken: cancellationToken);
            }
        }
    }
}
