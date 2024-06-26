﻿using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Internal;

namespace Tingle.EventBus;

/// <summary>
/// Contract describing a consumer of one or more events.
/// </summary>
public interface IEventConsumer
{
    // Intentionally left blank
}

/// <summary>
/// Contract describing a consumer of an event.
/// </summary>
public interface IEventConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T> : IEventConsumer where T : class
{
    /// <summary>
    /// Consume an event of the provided type.
    /// </summary>
    /// <param name="context">The context of the event</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ConsumeAsync(EventContext<T> context, CancellationToken cancellationToken);
}

/// <summary>
/// Contract describing a consumer of a dead-lettered event.
/// </summary>
public interface IDeadLetteredEventConsumer<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T> : IEventConsumer where T : class
{
    /// <summary>
    /// Consume a dead-lettered event of the provided type.
    /// </summary>
    /// <param name="context">The context of the event</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ConsumeAsync(DeadLetteredEventContext<T> context, CancellationToken cancellationToken);
}
