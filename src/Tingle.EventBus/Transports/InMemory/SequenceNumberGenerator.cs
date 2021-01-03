using System;
using System.Threading;

namespace Tingle.EventBus.Transports.InMemory
{
    /// <summary>
    /// A sequence number generator for Inmemory transport
    /// </summary>
    public class SequenceNumberGenerator // TODO: unit test this
    {
        private long currentValue;

        ///
        public SequenceNumberGenerator(Random random)
        {
            var bys = new byte[8];
            random.NextBytes(bys);
            currentValue = BitConverter.ToInt64(bys);
        }

        ///
        public SequenceNumberGenerator() : this(new Random()) { }

        ///
        public SequenceNumberGenerator(int seed) : this(new Random(Seed: seed)) { }

        /// <summary>
        /// Generate the next sequence number.
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            var v = Interlocked.Increment(ref currentValue);
            return Convert.ToUInt64(v).ToString();
        }
    }
}
