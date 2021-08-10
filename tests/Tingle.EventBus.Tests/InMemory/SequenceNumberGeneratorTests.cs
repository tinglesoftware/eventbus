using System;
using System.Threading.Tasks;
using Tingle.EventBus.Transports.InMemory;
using Xunit;

namespace Tingle.EventBus.Tests.InMemory
{
    public class SequenceNumberGeneratorTests
    {
        [Fact]
        public async Task Generate_Works()
        {
            var sng = new SequenceNumberGenerator();
            var current = sng.Generate();
            var currentUL = ulong.Parse(current);
            await Task.Delay(TimeSpan.FromSeconds(1));
            var next = sng.Generate();
            var nextUL = ulong.Parse(next);
            Assert.Equal(1UL, nextUL - currentUL);
        }
    }
}
