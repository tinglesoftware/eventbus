﻿using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus;
using Tingle.EventBus.Internal;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extensions on <see cref="ILogger"/> for the EventBus
/// </summary>
internal static partial class ILoggerExtensions
{
    [LoggerMessage(100, LogLevel.Information, "Sending {EventBusId} to '{StreamName}'. Scheduled: {Scheduled}.")]
    public static partial void SendingToStream(this ILogger logger, string? eventBusId, string streamName, DateTimeOffset? scheduled);

    [LoggerMessage(101, LogLevel.Information, "Sending {EventsCount} messages to '{StreamName}'. Scheduled: {Scheduled}. Events:\r\n- {EventBusIds}")]
    private static partial void SendingEventsToStream(this ILogger logger, int eventsCount, string streamName, DateTimeOffset? scheduled, string eventBusIds);

    public static void SendingEventsToStream(this ILogger logger, IList<string?> eventBusIds, string streamName, DateTimeOffset? scheduled)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.SendingEventsToStream(eventsCount: eventBusIds.Count,
                                     streamName: streamName,
                                     scheduled: scheduled,
                                     eventBusIds: string.Join("\r\n- ", eventBusIds));
    }

    public static void SendingEventsToStream<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(this ILogger logger, IList<EventContext<T>> events, string entityPath, DateTimeOffset? scheduled = null)
        where T : class
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        logger.SendingEventsToStream(events.Select(e => e.Id).ToList(), entityPath, scheduled);
    }

    [LoggerMessage(102, LogLevel.Warning, "Amazon Kinesis does not support delay or scheduled publish.")]
    public static partial void SchedulingNotSupported(this ILogger logger);
}
