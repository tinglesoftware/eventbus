using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports.Amazon.Kinesis
{
    /// <summary>
    /// Implementation of <see cref="IEventBusTransport"/> via <see cref="EventBusTransportBase{TTransportOptions}"/> using
    /// Amazon Kinesis as the transport.
    /// </summary>
    [TransportName(TransportNames.AmazonKinesis)]
    public class AmazonKinesisTransport : EventBusTransportBase<AmazonKinesisTransportOptions>
    {
        private readonly AmazonKinesisClient kinesisClient;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AmazonKinesisTransport(IServiceScopeFactory serviceScopeFactory,
                                      IOptions<EventBusOptions> busOptionsAccessor,
                                      IOptions<AmazonKinesisTransportOptions> transportOptionsAccessor,
                                      ILoggerFactory loggerFactory)
            : base(serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            kinesisClient = new AmazonKinesisClient(credentials: TransportOptions.Credentials,
                                                    clientConfig: TransportOptions.KinesisConfig);
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(Dictionary<string, object> data,
                                                          CancellationToken cancellationToken = default)
        {
            _ = await kinesisClient.ListStreamsAsync(cancellationToken);
            return true;
        }

        /// <inheritdoc/>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            // if there are consumers for this transport, throw exception
            var registrations = GetRegistrations();
            if (registrations.Count > 0)
            {
                // Consuming is not yet supported on this bus due to it's complexity
                throw new NotSupportedException("Amazon Kinesis does not support consumers yet due to complexity requirements from KCL.");
            }
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            // if there are consumers for this transport, throw exception
            var registrations = GetRegistrations();
            if (registrations.Count > 0)
            {
                // Consuming is not yet supported on this bus due to it's complexity
                throw new NotSupportedException("Amazon Kinesis does not support consumers yet due to complexity requirements from KCL.");
            }
        }

        /// <inheritdoc/>
        public override async Task<ScheduledResult?> PublishAsync<TEvent>(EventContext<TEvent> @event,
                                                                          EventRegistration registration,
                                                                          DateTimeOffset? scheduled = null,
                                                                          CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Amazon Kinesis does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            using var ms = new MemoryStream();
            await SerializeAsync(scope: scope,
                                 body: ms,
                                 @event: @event,
                                 registration: registration,
                                 cancellationToken: cancellationToken);

            // prepare the record
            var streamName = registration.EventName;
            var request = new PutRecordRequest
            {
                Data = ms,
                PartitionKey = TransportOptions.PartitionKeyResolver(@event),
                StreamName = streamName,
            };

            // send the event
            Logger.LogInformation("Sending {Id} to '{StreamName}'. Scheduled: {Scheduled}", @event.Id, streamName, scheduled);
            var response = await kinesisClient.PutRecordAsync(request, cancellationToken);
            response.EnsureSuccess();

            // return the sequence number
            return scheduled != null ? new ScheduledResult(id: response.SequenceNumber, scheduled: scheduled.Value) : null;
        }

        /// <inheritdoc/>
        public override async Task<IList<ScheduledResult>?> PublishAsync<TEvent>(IList<EventContext<TEvent>> events,
                                                                                 EventRegistration registration,
                                                                                 DateTimeOffset? scheduled = null,
                                                                                 CancellationToken cancellationToken = default)
        {
            // log warning when trying to publish scheduled message
            if (scheduled != null)
            {
                Logger.LogWarning("Amazon Kinesis does not support delay or scheduled publish");
            }

            using var scope = CreateScope();
            var records = new List<PutRecordsRequestEntry>();

            // work on each event
            foreach (var @event in events)
            {
                using var ms = new MemoryStream();
                await SerializeAsync(scope: scope,
                                     body: ms,
                                     @event: @event,
                                     registration: registration,
                                     cancellationToken: cancellationToken);

                var record = new PutRecordsRequestEntry
                {
                    Data = ms,
                    PartitionKey = TransportOptions.PartitionKeyResolver(@event),
                };
                records.Add(record);
            }

            // prepare the request
            var streamName = registration.EventName;
            var request = new PutRecordsRequest
            {
                StreamName = streamName,
                Records = records,
            };

            // send the events
            Logger.LogInformation("Sending {EventsCount} messages to '{StreamName}'. Scheduled: {Scheduled}. Events:\r\n- {Ids}",
                                  events.Count,
                                  streamName,
                                  scheduled,
                                  string.Join("\r\n- ", events.Select(e => e.Id)));
            var response = await kinesisClient.PutRecordsAsync(request, cancellationToken);
            response.EnsureSuccess();

            // Should we check for failed records and throw exception?

            // return the sequence numbers
            if (scheduled is not null)
            {
                return response.Records.Select(m => new ScheduledResult(id: m.SequenceNumber.ToString(), scheduled: scheduled.Value)).ToList();
            }
            else
            {
                return Array.Empty<ScheduledResult>();
            }
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Amazon Kinesis does not support canceling published events.");
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids,
                                                 EventRegistration registration,
                                                 CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Amazon Kinesis does not support canceling published events.");
        }
    }
}
