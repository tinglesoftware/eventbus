﻿using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus.Transports.Amazon.Kinesis;

/// <summary>
/// Implementation of <see cref="EventBusTransport{TOptions}"/> using
/// Amazon Kinesis as the transport.
/// </summary>
public class AmazonKinesisTransport : EventBusTransport<AmazonKinesisTransportOptions>
{
    private readonly Lazy<AmazonKinesisClient> kinesisClient;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="busOptionsAccessor"></param>
    /// <param name="optionsMonitor"></param>
    /// <param name="loggerFactory"></param>
    public AmazonKinesisTransport(IServiceScopeFactory serviceScopeFactory,
                                  IOptions<EventBusOptions> busOptionsAccessor,
                                  IOptionsMonitor<AmazonKinesisTransportOptions> optionsMonitor,
                                  ILoggerFactory loggerFactory)
        : base(serviceScopeFactory, busOptionsAccessor, optionsMonitor, loggerFactory)
    {
        kinesisClient = new Lazy<AmazonKinesisClient>(
            () => new AmazonKinesisClient(credentials: Options.Credentials, clientConfig: Options.KinesisConfig));
    }

    /// <inheritdoc/>
    protected override Task StartCoreAsync(CancellationToken cancellationToken)
    {
        // if there are consumers for this transport, throw exception
        var registrations = GetRegistrations();
        if (registrations.Count > 0)
        {
            // Consuming is not yet supported on this bus due to it's complexity
            throw new NotSupportedException("Amazon Kinesis does not support consumers yet due to complexity requirements from KCL.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override Task StopCoreAsync(CancellationToken cancellationToken)
    {
        // if there are consumers for this transport, throw exception
        var registrations = GetRegistrations();
        if (registrations.Count > 0)
        {
            // Consuming is not yet supported on this bus due to it's complexity
            throw new NotSupportedException("Amazon Kinesis does not support consumers yet due to complexity requirements from KCL.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async Task<ScheduledResult?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(EventContext<TEvent> @event,
                                                                                                                                EventRegistration registration,
                                                                                                                                DateTimeOffset? scheduled = null,
                                                                                                                                CancellationToken cancellationToken = default)
    {
        // log warning when trying to publish scheduled message
        if (scheduled != null)
        {
            Logger.SchedulingNotSupported();
        }

        var body = await SerializeAsync(@event: @event,
                                        registration: registration,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

        // prepare the record
        var streamName = registration.EventName!;
        var request = new PutRecordRequest
        {
            Data = body.ToMemoryStream(),
            PartitionKey = Options.PartitionKeyResolver(@event),
            StreamName = streamName,
        };

        // send the event
        Logger.SendingToStream(eventBusId: @event.Id, streamName: streamName, scheduled: scheduled);
        var response = await kinesisClient.Value.PutRecordAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccess();

        // return the sequence number
        return scheduled != null ? new ScheduledResult(id: response.SequenceNumber, scheduled: scheduled.Value) : null;
    }

    /// <inheritdoc/>
    protected override async Task<IList<ScheduledResult>?> PublishCoreAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IList<EventContext<TEvent>> events,
                                                                                                                                       EventRegistration registration,
                                                                                                                                       DateTimeOffset? scheduled = null,
                                                                                                                                       CancellationToken cancellationToken = default)
    {
        // log warning when trying to publish scheduled message
        if (scheduled != null)
        {
            Logger.SchedulingNotSupported();
        }

        var records = new List<PutRecordsRequestEntry>();

        // work on each event
        foreach (var @event in events)
        {
            var body = await SerializeAsync(@event: @event,
                                            registration: registration,
                                            cancellationToken: cancellationToken).ConfigureAwait(false);

            var record = new PutRecordsRequestEntry
            {
                Data = body.ToMemoryStream(),
                PartitionKey = Options.PartitionKeyResolver(@event),
            };
            records.Add(record);
        }

        // prepare the request
        var streamName = registration.EventName!;
        var request = new PutRecordsRequest
        {
            StreamName = streamName,
            Records = records,
        };

        // send the events
        Logger.SendingEventsToStream(events, streamName, scheduled);
        var response = await kinesisClient.Value.PutRecordsAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccess();

        // Should we check for failed records and throw exception?

        // return the sequence numbers
        if (scheduled is not null)
        {
            return response.Records.Select(m => new ScheduledResult(id: m.SequenceNumber, scheduled: scheduled.Value)).ToList();
        }
        else
        {
            return Array.Empty<ScheduledResult>();
        }
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(string id,
                                                    EventRegistration registration,
                                                    CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Amazon Kinesis does not support canceling published events.");
    }

    /// <inheritdoc/>
    protected override Task CancelCoreAsync<TEvent>(IList<string> ids,
                                             EventRegistration registration,
                                             CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Amazon Kinesis does not support canceling published events.");
    }
}
