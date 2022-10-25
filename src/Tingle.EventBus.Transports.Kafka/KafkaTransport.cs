using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Mime;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Diagnostics;

namespace Tingle.EventBus.Transports.Kafka;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using Kafka.
/// </summary>
public class KafkaTransport : EventBusTransport<KafkaTransportOptions>, IDisposable
{
    // the timeout used for non-async operations
    private static readonly TimeSpan StandardTimeout = TimeSpan.FromSeconds(30);

    private readonly Lazy<IProducer<string, byte[]>> producer; // producer instance is thread safe thus can be shared, and across topics
    private readonly Lazy<IConsumer<string, byte[]>> consumer; // consumer instance is thread safe thus can be shared, and across topics
    private readonly CancellationTokenSource stoppingCts = new();
    private readonly List<Task> receiverTasks = new();
    private readonly Lazy<IAdminClient> adminClient;
    private bool disposedValue;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="environment"></param>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public KafkaTransport(IHostEnvironment environment,
                          IServiceScopeFactory serviceScopeFactory,
                          IOptions<EventBusOptions> busOptionsAccessor,
                          IOptionsMonitor<KafkaTransportOptions> optionsMonitor,
                          ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory)
    {
        // Should be setup the logger?
        adminClient = new Lazy<IAdminClient>(() => new AdminClientBuilder(Options.AdminConfig).Build());

        // create the shared producer instance
        producer = new Lazy<IProducer<string, byte[]>>(() =>
        {
            var pconfig = new ProducerConfig(Options.AdminConfig);
            return new ProducerBuilder<string, byte[]>(pconfig)
                            //.SetValueSerializer((ISerializer<byte[]>)null)
                            .Build();
        });

        // create the shared consumer instance
        consumer = new Lazy<IConsumer<string, byte[]>>(() =>
        {
            var c_config = new ConsumerConfig(Options.AdminConfig)
            {
                GroupId = BusOptions.Naming.GetApplicationName(environment),
                //EnableAutoCommit = false,
                //StatisticsIntervalMs = 5000,
                //SessionTimeoutMs = 6000,
                //AutoOffsetReset = AutoOffsetReset.Earliest,
                EnablePartitionEof = true,
            };
            return new ConsumerBuilder<string, byte[]>(c_config)
                            //.SetValueSerializer((ISerializer<byte[]>)null)
                            .Build();
        });
    }

    /// <inheritdoc/>
    protected override Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var registrations = GetRegistrations();
        var topics = registrations.Where(r => r.Consumers.Count > 0) // filter out those with consumers
                                  .Select(r => r.EventName) // pick the event name which is also the topic name
                                  .ToList();
        // only consume if there are topics to subscribe to
        if (topics.Count > 0)
        {
            consumer.Value.Subscribe(topics);
            _ = ProcessAsync(cancellationToken: stoppingCts.Token);
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
            // ensure all outstanding produce requests are processed
            producer.Value.Flush(cancellationToken);

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
    protected async override Task<ScheduledResult?> PublishCoreAsync<TEvent>(EventContext<TEvent> @event,
                                                                             EventRegistration registration,
                                                                             DateTimeOffset? scheduled = null,
                                                                             CancellationToken cancellationToken = default)
    {
        // log warning when trying to publish scheduled message
        if (scheduled != null)
        {
            Logger.SchedulingNotSupported();
        }

        using var scope = CreateScope();
        var body = await SerializeAsync(scope: scope,
                                        @event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

        // prepare the message
        var message = new Message<string, byte[]>();
        message.Headers.AddIfNotNull(MetadataNames.CorrelationId, @event.CorrelationId)
                       .AddIfNotNull(MetadataNames.ContentType, @event.ContentType?.ToString())
                       .AddIfNotNull(MetadataNames.RequestId, @event.RequestId)
                       .AddIfNotNull(MetadataNames.InitiatorId, @event.InitiatorId)
                       .AddIfNotNull(MetadataNames.ActivityId, Activity.Current?.Id);
        message.Key = @event.Id!;
        message.Value = body.ToArray();

        // send the event
        var topic = registration.EventName;
        var result = await producer.Value.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken).ConfigureAwait(false);
        // Should we check persistence status?

        // return the sequence number
        return scheduled != null ? new ScheduledResult(id: result.Offset.Value, scheduled: scheduled.Value) : null;
    }

    /// <inheritdoc/>
    protected async override Task<IList<ScheduledResult>?> PublishCoreAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                    EventRegistration registration,
                                                                                    DateTimeOffset? scheduled = null,
                                                                                    CancellationToken cancellationToken = default)
    {
        // log warning when doing batch
        Logger.BatchingNotSupported();

        // log warning when trying to publish scheduled message
        if (scheduled != null)
        {
            Logger.SchedulingNotSupported();
        }

        using var scope = CreateScope();
        var sequenceNumbers = new List<long>();

        // work on each event
        foreach (var @event in events)
        {
            var body = await SerializeAsync(scope: scope,
                                            @event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);

            // prepare the message
            var message = new Message<string, byte[]>();
            message.Headers.AddIfNotNull(MetadataNames.CorrelationId, @event.CorrelationId)
                           .AddIfNotNull(MetadataNames.ContentType, @event.ContentType?.ToString())
                           .AddIfNotNull(MetadataNames.RequestId, @event.RequestId)
                           .AddIfNotNull(MetadataNames.InitiatorId, @event.InitiatorId)
                           .AddIfNotNull(MetadataNames.ActivityId, Activity.Current?.Id);
            message.Key = @event.Id!;
            message.Value = body.ToArray();

            // send the event
            var topic = registration.EventName;
            var result = await producer.Value.ProduceAsync(topic: topic, message: message, cancellationToken: cancellationToken).ConfigureAwait(false);
            // Should we check persistence status?

            // collect the sequence number
            sequenceNumbers.Add(result.Offset.Value);
        }

        // return the sequence numbers
        return scheduled != null ? sequenceNumbers.Select(n => new ScheduledResult(id: n, scheduled: scheduled.Value)).ToList() : null;
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(string id,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Kafka does not support canceling published events.");
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(IList<string> ids,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Kafka does not support canceling published events.");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var mt = GetType().GetMethod(nameof(OnEventReceivedAsync), flags) ?? throw new InvalidOperationException("Method should not be null");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Value.Consume(cancellationToken);
                if (result.IsPartitionEOF)
                {
                    Logger.EndOfTopic(result.Topic, result.Partition, result.Offset);
                    continue;
                }

                Logger.ConsumerReceivedData(result.TopicPartitionOffset);

                // get the registration for topic
                var topic = result.Topic;
                var reg = GetRegistrations().Single(r => r.EventName == topic);

                // form the generic method
                var ecr = reg.Consumers.Single(); // only one consumer per event
                var method = mt.MakeGenericMethod(reg.EventType, ecr.ConsumerType);
                await ((Task)method.Invoke(this, new object[] { reg, ecr, result, cancellationToken, })!).ConfigureAwait(false);


                // if configured to checkpoint at intervals, respect it
                if ((result.Offset % Options.CheckpointInterval) == 0)
                {
                    // The Commit method sends a "commit offsets" request to the Kafka
                    // cluster and synchronously waits for the response. This is very
                    // slow compared to the rate at which the consumer is capable of
                    // consuming messages. A high performance application will typically
                    // commit offsets relatively infrequently and be designed handle
                    // duplicate messages in the event of failure.
                    consumer.Value.Commit(result);
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
                                                               EventConsumerRegistration ecr,
                                                               ConsumeResult<string, byte[]> result,
                                                               CancellationToken cancellationToken)
        where TEvent : class
        where TConsumer : IEventConsumer<TEvent>
    {
        var message = result.Message;
        var messageKey = message.Key;
        message.Headers.TryGetValue(MetadataNames.CorrelationId, out var correlationId);
        message.Headers.TryGetValue(MetadataNames.ContentType, out var contentType_str);
        message.Headers.TryGetValue(MetadataNames.ActivityId, out var parentActivityId);

        using var log_scope = BeginLoggingScopeForConsume(id: messageKey, correlationId: correlationId);

        // Instrumentation
        using var activity = EventBusActivitySource.StartActivity(ActivityNames.Consume, ActivityKind.Consumer, parentActivityId);
        activity?.AddTag(ActivityTagNames.EventBusEventType, typeof(TEvent).FullName);
        activity?.AddTag(ActivityTagNames.EventBusConsumerType, typeof(TConsumer).FullName);
        activity?.AddTag(ActivityTagNames.MessagingSystem, Name);

        Logger.ProcessingMessage(messageKey);
        using var scope = CreateScope();
        var contentType = contentType_str == null ? null : new ContentType(contentType_str);
        var context = await DeserializeAsync<TEvent>(scope: scope,
                                                     body: new BinaryData(message.Value),
                                                     contentType: contentType,
                                                     registration: reg,
                                                     identifier: result.Offset.ToString(),
                                                     raw: message,
                                                     cancellationToken: cancellationToken).ConfigureAwait(false);
        Logger.ReceivedEvent(messageKey, context.Id);
        var (successful, _) = await ConsumeAsync<TEvent, TConsumer>(registration: reg,
                                                                    ecr: ecr,
                                                                    @event: context,
                                                                    scope: scope,
                                                                    cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!successful && ecr.UnhandledErrorBehaviour == UnhandledConsumerErrorBehaviour.Deadletter)
        {
            // produce message on dead-letter topic
            var dlt = reg.EventName += Options.DeadLetterSuffix;
            await producer.Value.ProduceAsync(topic: dlt, message: message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // TODO: find a better way to handle the checkpointing when there is an error
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
