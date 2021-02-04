using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tingle.EventBus.Tests
{
    public class EventBusNamingOptionsTests
    {
        [Theory]
        [InlineData("Tingle.EventBus.Sample1", NamingConvention.KebabCase, "dev", "dev-tingle-event-bus-sample1")]
        [InlineData("Tingle.EventBus.Sample1", NamingConvention.SnakeCase, "prod", "prod_tingle_event_bus_sample1")]
        public void GetApplicationName_Works(string applicationName, NamingConvention convention, string scope, string expected)
        {
            var options = new EventBusNamingOptions { Scope = scope, Convention = convention, };
            var environment = new FakeHostEnvironment(applicationName);
            var actual = options.GetApplicationName(environment);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("DoorOpenedEvent", "DoorOpened")]
        [InlineData("DoorOpenedConsumer", "DoorOpened")]
        [InlineData("DoorOpenedEventConsumer", "DoorOpened")]
        [InlineData("DoorOpened", "DoorOpened")] // unchanged
        public void TrimCommonSuffixes_Works(string typeName, string expected)
        {
            var options = new EventBusOptions { };
            options.Naming.TrimTypeNames = true;
            var actual = options.Naming.TrimCommonSuffixes(typeName);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("SampleEvent", NamingConvention.KebabCase, "sample-event")]
        [InlineData("SampleConsumer", NamingConvention.SnakeCase, "sample_consumer")]
        public void ApplyNamingConvention_Works(string raw, NamingConvention convention, string expected)
        {
            var options = new EventBusNamingOptions { Convention = convention, };
            var actual = options.ApplyNamingConvention(raw);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("dev-sample.event", NamingConvention.KebabCase, "dev-sample-event")]
        [InlineData("prd_sample+event", NamingConvention.SnakeCase, "prd_sample_event")]
        public void ReplaceInvalidCharacters_Works(string raw, NamingConvention convention, string expected)
        {
            var options = new EventBusNamingOptions { Convention = convention, };
            var actual = options.ReplaceInvalidCharacters(raw);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("sample-event", NamingConvention.KebabCase, "dev", "dev-sample-event")]
        [InlineData("sample_event", NamingConvention.SnakeCase, "prd", "prd_sample_event")]
        public void AppendScope_Works(string unscoped, NamingConvention convention, string scope, string expected)
        {
            var options = new EventBusNamingOptions { Convention = convention, Scope = scope, };
            var actual = options.AppendScope(unscoped);
            Assert.Equal(expected, actual);
        }
    }
}
