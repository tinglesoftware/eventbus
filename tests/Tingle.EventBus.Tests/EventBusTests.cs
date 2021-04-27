using System;
using Tingle.EventBus.Registrations;
using Xunit;

namespace Tingle.EventBus.Tests
{
    public class EventBusTests
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
            var reg = new EventRegistration(typeof(TestEvent1)) { IdFormat = format, };
            var id = EventBus.GenerateEventId(reg);
            Assert.NotNull(id);
        }

        [Fact]
        public void GenerateEventId_Throws_InvalidOperationExecption()
        {
            var reg = new EventRegistration(typeof(TestEvent1)) { IdFormat = null, };
            var ex = Assert.Throws<NotSupportedException>(() => EventBus.GenerateEventId(reg));
            Assert.Equal($"'{nameof(EventIdFormat)}.{reg.IdFormat}' set on event '{reg.EventType.FullName}' is not supported.", ex.Message);
        }
    }
}
