using System;
using System.Threading;

namespace Tingle.EventBus.Transports.InMemory
{
    /// <summary>
    /// A sequence number generator for Inmemory transport
    /// </summary>
    public class SequenceNumberGenerator
    {
        private int currentValue;

        ///
        public SequenceNumberGenerator()
        {
            var random = new Random(DateTimeOffset.UtcNow.Millisecond);
            currentValue = random.Next();
        }

        /// <summary>
        /// Generate the next sequence number.
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            var v = Interlocked.Increment(ref currentValue);
            return ((ulong)v).ToString();
        }
    }
}
