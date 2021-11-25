using System;
using System.Collections.Generic;

namespace Tingle.EventBus.Retries;

/// <summary>Abstract implementation of <see cref="IRetryableEvent"/>.</summary>
public abstract record AbstractRetryableEvent : IRetryableEvent
{
    /// <summary>
    /// 
    /// </summary>
    protected AbstractRetryableEvent() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delays">Value for <see cref="Delays"/>.</param>
    protected AbstractRetryableEvent(IList<TimeSpan> delays)
    {
        Delays = delays ?? throw new ArgumentNullException(nameof(delays));
        if (delays.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delays), "You must have at least one delay entry.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delays">Value for <see cref="Delays"/>.</param>
    /// <param name="attempts">Value for <see cref="Attempts"/>.</param>
    protected AbstractRetryableEvent(IList<TimeSpan> delays, IList<DateTimeOffset> attempts) : this(delays)
    {
        Attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
    }

    /// <inheritdoc/>
    public IList<TimeSpan> Delays { get; set; } = new List<TimeSpan>();

    /// <inheritdoc/>
    public IList<DateTimeOffset> Attempts { get; set; } = new List<DateTimeOffset>();
}
