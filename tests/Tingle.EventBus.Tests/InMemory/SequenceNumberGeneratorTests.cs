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
            await Task.Delay(TimeSpan.FromSeconds(1));
            var next = sng.Generate();
            Assert.Equal(1, next - current);
        }
    }
}
