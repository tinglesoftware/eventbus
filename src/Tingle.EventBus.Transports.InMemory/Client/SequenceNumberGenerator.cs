using System;
using System.Threading;

namespace Tingle.EventBus.Transports.InMemory.Client;

/// <summary>
/// A sequence number generator for InMemory transport
/// </summary>
public class SequenceNumberGenerator
{
    private long currentValue;

    ///
    public SequenceNumberGenerator()
    {
        var random = new Random(DateTimeOffset.UtcNow.Millisecond);
        currentValue = random.Next();
    }

    /// <summary>Generate the next sequence number.</summary>
    public long Generate() => Interlocked.Increment(ref currentValue);
}
