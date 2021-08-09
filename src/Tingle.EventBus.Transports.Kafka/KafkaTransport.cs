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
    public class KafkaTransport : EventBusTransportBase<KafkaTransportOptions>, IDisposable
    {
        // the timeout used for non-async operations
        private static readonly TimeSpan StandardTimeout = TimeSpan.FromSeconds(30);

        private readonly IProducer<string, byte[]> producer; // producer instance is thread safe thus can be shared, and across topics
        private readonly IConsumer<string, byte[]> consumer; // consumer instance is thread safe thus can be shared, and across topics
        private readonly CancellationTokenSource stoppingCts = new();
        private readonly List<Task> receiverTasks = new();
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
                GroupId = BusOptions.Naming.GetApplicationName(environment),
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
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            var registrations = GetRegistrations();
            var topics = registrations.Where(r => r.Consumers.Count > 0) // filter out those with consumers
                                      .Select(r => r.EventName) // pick the event name which is also the topic name
                                      .ToList();
            // only consume if there are topics to subscribe to
            if (topics.Count > 0)
            {
                consumer.Subscribe(topics);
                _ = ProcessAsync(cancellationToken: stoppingCts.Token);
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
                // ensure all outstanding produce requests are processed
                producer.Flush(cancellationToken);

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
        public async override Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                          EventRegistration registration,
                                                                          DateTimeOffset? scheduled = null,
                                                                          CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Kafka does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            using var ms = new MemoryStream();
            await SerializeAsync(body: ms,
                                 @event: @event,
                                 registration: registration,
                                 scope: scope,
                                 cancellationToken: cancellationToken);

            // prepare the message
            var message = new Message<string, byte[]>();
            message.Headers.AddIfNotNull(AttributeNames.CorrelationId, @event.CorrelationId)
                           .AddIfNotNull(AttributeNames.ContentType, @event.ContentType?.ToString())
                           .AddIfNotNull(AttributeNames.RequestId, @event.RequestId)
                           .AddIfNotNull(AttributeNames.InitiatorId, @event.InitiatorId)
                           .AddIfNotNull(AttributeNames.ActivityId, Activity.Current?.Id);
            message.Key = @event.Id!;
            message.Value = ms.ToArray();

            // send the event
            var topic = registration.EventName;
            var result = await producer.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken);
            // Should we check persistence status?

            // return the sequence number
            return scheduled != null ? new ScheduledResult(id: result.Offset.Value.ToString(), scheduled: scheduled.Value) : null;
        }

        /// <inheritdoc/>
        public async override Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                 EventRegistration registration,
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
            var sequenceNumbers = new List<string>();

            // work on each event
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                await SerializeAsync(body: ms,
                                     @event: @event,
                                     registration: registration,
                                     scope: scope,
                                     cancellationToken: cancellationToken);

                // prepare the message
                var message = new Message<string, byte[]>();
                message.Headers.AddIfNotNull(AttributeNames.CorrelationId, @event.CorrelationId)
                               .AddIfNotNull(AttributeNames.ContentType, @event.ContentType?.ToString())
                               .AddIfNotNull(AttributeNames.RequestId, @event.RequestId)
                               .AddIfNotNull(AttributeNames.InitiatorId, @event.InitiatorId)
                               .AddIfNotNull(AttributeNames.ActivityId, Activity.Current?.Id);
                message.Key = @event.Id!;
                message.Value = ms.ToArray();

                // send the event
                var topic = registration.EventName;
                var result = await producer.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken);
                // Should we check persistence status?

                // collect the sequence number
                sequenceNumbers.Add(result.Offset.Value.ToString());
            }

            // return the sequence numbers
            return scheduled != null ? sequenceNumbers.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList() : null;
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Kafka does not support canceling published events.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Kafka does not support canceling published events.");
        }

        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var mt = GetType().GetMethod(nameof(OnEventReceivedAsync), flags);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
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
                    var ereg = GetRegistrations().Single(r => r.EventName == topic);

                    // form the generic method
                    var creg = ereg.Consumers.Single(); // only one consumer per event
                    var method = mt.MakeGenericMethod(ereg.EventType, creg.ConsumerType);
                    await (Task)method.Invoke(this, new object[] { ereg, creg, result.Message, cancellationToken, });


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

        private async Task OnEventReceivedAsync<TEvent, TConsumer>(EventRegistration reg,
                                                                   EventConsumerRegistration creg,
                                                                   Message<string, byte[]> message,
                                                                   CancellationToken cancellationToken)
            where TEvent : class
            where TConsumer : IEventConsumer<TEvent>
        {
            var messageKey = message.Key;
            message.Headers.TryGetValue(AttributeNames.CorrelationId, out var correlationId);
            message.Headers.TryGetValue(AttributeNames.ContentType, out var contentType_str);
            message.Headers.TryGetValue(AttributeNames.ActivityId, out var parentActivityId);

            using var log_scope = BeginLoggingScopeForConsume(id: messageKey, correlationId: correlationId);

            // Instrumentation
            using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId?.ToString());
            activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
            activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
            activity?.AddTag(ActivityTagNames.MessagingSystem, Name);

            Logger.LogDebug("Processing '{MessageKey}", messageKey);
            using var scope = CreateScope();
            using var ms = new MemoryStream(message.Value);
            var contentType = contentType_str == null ? null : new ContentType(contentType_str.ToString());
            var context = await DeserializeAsync<TEvent>(body: ms,
                                                         contentType: contentType,
                                                         registration: reg,
                                                         scope: scope,
                                                         identifier: messageKey, // TODO: use offset
                                                         cancellationToken: cancellationToken);
            Logger.LogInformation("Received event: '{MessageKey}' containing Event '{Id}'",
                                  messageKey,
                                  context.Id);
            var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(creg: creg,
                                                                        @event: context,
                                                                        scope: scope,
                                                                        cancellationToken: cancellationToken);

            if (!successful && creg.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
            {
                // produce message on deadletter topic
                var dlt = reg.EventName += TransportOptions.DeadLetterSuffix;
                await producer.ProduceAsync(topic: dlt, message: message, cancellationToken: cancellationToken);
            }

            // TODO: find a better way to handle the checkpointing when there is an error
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            stoppingCts.Cancel();
        }
    }
}
