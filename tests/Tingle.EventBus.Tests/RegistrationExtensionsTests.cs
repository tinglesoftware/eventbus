using System;
using Tingle.EventBus.Registrations;
using Tingle.EventBus.Serialization;
using Xunit;

namespace Tingle.EventBus.Tests
{
    public class RegistrationExtensionsTests
    {
        [Theory]
        [InlineData("SampleEvent", NamingConvention.KebabCase, "sample-event")]
        [InlineData("SampleConsumer", NamingConvention.SnakeCase, "sample_consumer")]
        public void ApplyNamingConvention_Works(string raw, NamingConvention convention, string expected)
        {
            var actual = RegistrationExtensions.ApplyNamingConvention(raw: raw, convention: convention);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("sample-event", NamingConvention.KebabCase, "dev", "dev-sample-event")]
        [InlineData("sample_event", NamingConvention.SnakeCase, "prd", "prd_sample_event")]
        public void AppendScope_Works(string unscoped, NamingConvention convention, string scope, string expected)
        {
            var actual = RegistrationExtensions.AppendScope(unscoped: unscoped, convention: convention, scope: scope);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("dev-sample.event", NamingConvention.KebabCase, "dev-sample-event")]
        [InlineData("prd_sample+event", NamingConvention.SnakeCase, "prd_sample_event")]
        public void ReplaceInvalidCharacters_Works(string raw, NamingConvention convention, string expected)
        {
            var actual = RegistrationExtensions.ReplaceInvalidCharacters(raw: raw, convention: convention);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SetSerializer_UsesDefault()
        {
            // when not set, use default
            var registration = new EventRegistration(typeof(TestEvent1));
            Assert.Null(registration.EventSerializerType);
            registration.SetSerializer();
            Assert.Equal(typeof(IEventSerializer), registration.EventSerializerType);
        }

        [Fact]
        public void SetSerializer_RepsectsAttribute()
        {
            // attribute is respected
            var registration = new EventRegistration(typeof(TestEvent2));
            registration.SetSerializer();
            Assert.Equal(typeof(FakeEventSerializer1), registration.EventSerializerType);
        }

        [Fact]
        public void SetSerializer_Throws_InvalidOperationException()
        {
            // attribute is respected
            var registration = new EventRegistration(typeof(TestEvent3));
            var ex = Assert.Throws<InvalidOperationException>(() => registration.SetSerializer());
            Assert.Equal("The type 'Tingle.EventBus.Tests.FakeEventSerializer2' is used"
                       + " as a serializer but does not implement 'Tingle.EventBus.Serialization.IEventSerializer'",
                ex.Message);
        }

        [Theory]
        [InlineData("DoorOpenedEvent", "DoorOpened")]
        [InlineData("DoorOpenedConsumer", "DoorOpened")]
        [InlineData("DoorOpenedEventConsumer", "DoorOpened")]
        [InlineData("DoorOpened", "DoorOpened")] // unchanged
        public void TrimCommonSuffixes_Works(string typeName, string expected)
        {
            var options = new EventBusOptions { TrimTypeNames = true, };
            var actual = options.TrimCommonSuffixes(typeName);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(typeof(TestEvent1), false, "dev", NamingConvention.KebabCase, "dev-test-event1")]
        [InlineData(typeof(TestEvent1), false, "dev", NamingConvention.SnakeCase, "dev_test_event1")]
        [InlineData(typeof(TestEvent1), true, "dev", NamingConvention.KebabCase, "dev-tingle-event-bus-tests-test-event1")]
        [InlineData(typeof(TestEvent1), true, "dev", NamingConvention.SnakeCase, "dev_tingle_event_bus_tests_test_event1")]
        [InlineData(typeof(TestEvent2), true, "dev", NamingConvention.KebabCase, "sample-event")]
        public void SetEventName_Works(Type eventType, bool useFullTypeNames, string scope, NamingConvention namingConvention, string expected)
        {
            var options = new EventBusOptions
            {
                UseFullTypeNames = useFullTypeNames,
                Scope = scope,
                NamingConvention = namingConvention,
            };
            var registration = new EventRegistration(eventType);
            registration.SetEventName(options);
            Assert.Equal(expected, registration.EventName);
        }

        [Theory]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, ConsumerNameSource.TypeName, NamingConvention.KebabCase, "test-consumer1-test-event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, ConsumerNameSource.TypeName, NamingConvention.SnakeCase, "test_consumer1_test_event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, ConsumerNameSource.ApplicationName, NamingConvention.KebabCase, "app1-test-event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, ConsumerNameSource.ApplicationName, NamingConvention.SnakeCase, "app1_test_event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, ConsumerNameSource.ApplicationAndTypeName, NamingConvention.KebabCase, "app1-test-consumer1-test-event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, ConsumerNameSource.ApplicationAndTypeName, NamingConvention.SnakeCase, "app1_test_consumer1_test_event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, ConsumerNameSource.TypeName, NamingConvention.KebabCase, "tingle-event-bus-tests-test-consumer1-tingle-event-bus-tests-test-event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, ConsumerNameSource.TypeName, NamingConvention.SnakeCase, "tingle_event_bus_tests_test_consumer1_tingle_event_bus_tests_test_event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, ConsumerNameSource.ApplicationName, NamingConvention.KebabCase, "app1-tingle-event-bus-tests-test-event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, ConsumerNameSource.ApplicationName, NamingConvention.SnakeCase, "app1_tingle_event_bus_tests_test_event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, ConsumerNameSource.ApplicationAndTypeName, NamingConvention.KebabCase, "app1-tingle-event-bus-tests-test-consumer1-tingle-event-bus-tests-test-event1")]
        [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, ConsumerNameSource.ApplicationAndTypeName, NamingConvention.SnakeCase, "app1_tingle_event_bus_tests_test_consumer1_tingle_event_bus_tests_test_event1")]
        [InlineData(typeof(TestEvent2), typeof(TestConsumer2), false, ConsumerNameSource.TypeName, NamingConvention.SnakeCase, "sample-consumer_sample-event")]
        [InlineData(typeof(TestEvent2), typeof(TestConsumer2), false, ConsumerNameSource.ApplicationName, NamingConvention.SnakeCase, "sample-consumer_sample-event")]
        [InlineData(typeof(TestEvent2), typeof(TestConsumer2), false, ConsumerNameSource.ApplicationAndTypeName, NamingConvention.SnakeCase, "sample-consumer_sample-event")]
        [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, ConsumerNameSource.TypeName, NamingConvention.KebabCase, "sample-consumer-sample-event")]
        [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, ConsumerNameSource.ApplicationName, NamingConvention.KebabCase, "sample-consumer-sample-event")]
        [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, ConsumerNameSource.ApplicationAndTypeName, NamingConvention.KebabCase, "sample-consumer-sample-event")]
        public void SetConsumerName_Works(Type eventType, Type consumerType, bool useFullTypeNames, ConsumerNameSource consumerNameSource, NamingConvention namingConvention, string expected)
        {
            var environment = new FakeHostEnvironment("app1");
            var options = new EventBusOptions
            {
                UseFullTypeNames = useFullTypeNames,
                ConsumerNameSource = consumerNameSource,
                NamingConvention = namingConvention,
            };

            var registration = new EventRegistration(eventType);
            registration.Consumers.Add(new EventConsumerRegistration(consumerType));

            var creg = Assert.Single(registration.Consumers);
            registration.SetEventName(options)
                        .SetConsumerNames(options, environment);
            Assert.Equal(expected, creg.ConsumerName);
        }
    }
}
