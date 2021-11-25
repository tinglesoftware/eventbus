using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Retries;

/// <summary>
/// Represents an event (or command) that can be retried and carries its own retry schedule.
/// </summary>
public interface IRetryableEvent
{
    /// <summary>List of delays to be used for this command.</summary>
    IList<TimeSpan> Delays { get; set; }

    /// <summary>List of previous attempts.</summary>
    IList<DateTimeOffset> Attempts { get; set; }
}
