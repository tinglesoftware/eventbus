using System;
using Tingle.EventBus.Ids;
using Tingle.EventBus.Registrations;
using Xunit;

namespace Tingle.EventBus.Tests
{
    public class DefaultEventIdGeneratorTests
    {
        [Theory]
        [InlineData(EventIdFormat.Guid)]
        [InlineData(EventIdFormat.GuidNoDashes)]
        [InlineData(EventIdFormat.Long)]
        [InlineData(EventIdFormat.LongHex)]
        [InlineData(EventIdFormat.DoubleLong)]
        [InlineData(EventIdFormat.DoubleLongHex)]
        [InlineData(EventIdFormat.Random)]
        public void GenerateEventId_Works(EventIdFormat format)
        {
            var generator = new DefaultEventIdGenerator();
            var reg = new EventRegistration(typeof(TestEvent1)) { IdFormat = format, };
            var id = generator.Generate(reg);
            Assert.NotNull(id);
        }

        [Fact]
        public void GenerateEventId_Throws_InvalidOperationExecption()
        {
            var generator = new DefaultEventIdGenerator();
            var reg = new EventRegistration(typeof(TestEvent1)) { IdFormat = null, };
            var ex = Assert.Throws<NotSupportedException>(() => generator.Generate(reg));
            Assert.Equal($"'{nameof(EventIdFormat)}.{reg.IdFormat}' set on event '{reg.EventType.FullName}' is not supported.", ex.Message);
        }
    }
}
