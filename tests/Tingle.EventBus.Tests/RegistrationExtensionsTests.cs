using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
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
            Assert.Equal(typeof(DummyEventSerializer1), registration.EventSerializerType);
        }

        [Fact]
        public void SetSerializer_Throws_InvalidOperationException()
        {
            // attribute is respected
            var registration = new EventRegistration(typeof(TestEvent3));
            var ex = Assert.Throws<InvalidOperationException>(() => registration.SetSerializer());
            Assert.Equal("The type 'Tingle.EventBus.Tests.RegistrationExtensionsTests+DummyEventSerializer2' is used"
                       + " as a serializer but does not implement 'Tingle.EventBus.Serialization.IEventSerializer'",
                ex.Message);
        }

        [Theory]
        [MemberData(nameof(SetEventNameData))]
        public void SetEventName_Works(EventRegistration registration, EventBusOptions options, string expected)
        {
            registration.SetEventName(options);
            Assert.Equal(expected, registration.EventName);
        }

        public static IEnumerable<object[]> SetEventNameData = new List<object[]>
        {
            new object[] {
                new EventRegistration(typeof(TestEvent1)),
                new EventBusOptions { UseFullTypeNames = false, Scope = "dev", NamingConvention = NamingConvention.KebabCase, },
                "dev-test-event1",
            },
            new object[] {
                new EventRegistration(typeof(TestEvent1)),
                new EventBusOptions { UseFullTypeNames = false, Scope = "dev", NamingConvention = NamingConvention.SnakeCase, },
                "dev_test_event1",
            },

            new object[] {
                new EventRegistration(typeof(TestEvent1)),
                new EventBusOptions { UseFullTypeNames = true, Scope = "dev", NamingConvention = NamingConvention.KebabCase, },
                "dev-tingle-event-bus-tests-registration-extensions-tests-test-event1",
            },
            new object[] {
                new EventRegistration(typeof(TestEvent1)),
                new EventBusOptions { UseFullTypeNames = true, Scope = "dev", NamingConvention = NamingConvention.SnakeCase, },
                "dev_tingle_event_bus_tests_registration_extensions_tests_test_event1",
            },

            new object[] {
                new EventRegistration(typeof(TestEvent2)),
                new EventBusOptions { UseFullTypeNames = true, Scope = "dev", NamingConvention = NamingConvention.KebabCase, },
                "sample-event",
            },
        };

        [Theory]
        [MemberData(nameof(SetConsumerNameData))]
        public void SetConsumerName(ConsumerRegistration registration, EventBusOptions options, string applicationName, string expected)
        {
            var environment = new DummyEnvironment(applicationName);
            registration.SetEventName(options)
                        .SetConsumerName(options, environment);
            Assert.Equal(expected, registration.ConsumerName);
        }

        public static IEnumerable<object[]> SetConsumerNameData = new List<object[]>
        {
            // UseFullTypeNames=false
            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.KebabCase },
                "app1",
                "test-consumer1-test-event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.SnakeCase },
                "app1",
                "test_consumer1_test_event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.ApplicationName, NamingConvention = NamingConvention.KebabCase },
                "app1",
                "app1-test-event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.ApplicationName, NamingConvention = NamingConvention.SnakeCase },
                "app1",
                "app1_test_event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.ApplicationAndTypeName, NamingConvention = NamingConvention.KebabCase },
                "app1",
                "app1-test-consumer1-test-event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.ApplicationAndTypeName, NamingConvention = NamingConvention.SnakeCase },
                "app1",
                "app1_test_consumer1_test_event1",
            },


            // UseFullTypeNames=false
            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.KebabCase },
                "app1",
                "tingle-event-bus-tests-registration-extensions-tests-test-consumer1-tingle-event-bus-tests-registration-extensions-tests-test-event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.SnakeCase },
                "app1",
                "tingle_event_bus_tests_registration_extensions_tests_test_consumer1_tingle_event_bus_tests_registration_extensions_tests_test_event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.ApplicationName, NamingConvention = NamingConvention.KebabCase },
                "app1",
                "app1-tingle-event-bus-tests-registration-extensions-tests-test-event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.ApplicationName, NamingConvention = NamingConvention.SnakeCase },
                "app1",
                "app1_tingle_event_bus_tests_registration_extensions_tests_test_event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.ApplicationAndTypeName, NamingConvention = NamingConvention.KebabCase },
                "app1",
                "app1-tingle-event-bus-tests-registration-extensions-tests-test-consumer1-tingle-event-bus-tests-registration-extensions-tests-test-event1",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent1), typeof(TestConsumer1)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.ApplicationAndTypeName, NamingConvention = NamingConvention.SnakeCase },
                "app1",
                "app1_tingle_event_bus_tests_registration_extensions_tests_test_consumer1_tingle_event_bus_tests_registration_extensions_tests_test_event1",
            },

            // Override Names
            new object[] {
                new ConsumerRegistration(typeof(TestEvent2), typeof(TestConsumer2)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.KebabCase, },
                "app1",
                "sample-consumer-sample-event",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent2), typeof(TestConsumer2)),
                new EventBusOptions { UseFullTypeNames = false, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.SnakeCase, },
                "app1",
                "sample-consumer_sample-event",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent2), typeof(TestConsumer2)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.KebabCase, },
                "app1",
                "sample-consumer-sample-event",
            },

            new object[] {
                new ConsumerRegistration(typeof(TestEvent2), typeof(TestConsumer2)),
                new EventBusOptions { UseFullTypeNames = true, ConsumerNameSource = ConsumerNameSource.TypeName, NamingConvention = NamingConvention.SnakeCase, },
                "app1",
                "sample-consumer_sample-event",
            },
        };

        class TestEvent1
        {
            public string Value1 { get; set; }
            public string Value2 { get; set; }
        }

        [EventName("sample-event")]
        [EventSerializer(typeof(DummyEventSerializer1))]
        class TestEvent2
        {
            public string Value1 { get; set; }
            public string Value2 { get; set; }
        }

        [EventSerializer(typeof(DummyEventSerializer2))]
        class TestEvent3
        {
            public string Value1 { get; set; }
            public string Value2 { get; set; }
        }

        class DummyEventSerializer1 : IEventSerializer
        {
            public Task<EventContext<T>> DeserializeAsync<T>(Stream stream, ContentType contentType, CancellationToken cancellationToken = default) where T : class
            {
                throw new NotImplementedException();
            }

            public Task SerializeAsync<T>(Stream stream, EventContext<T> context, HostInfo hostInfo, CancellationToken cancellationToken = default) where T : class
            {
                throw new NotImplementedException();
            }
        }

        class DummyEventSerializer2 { } // should not implement IEventSerializer

        class TestConsumer1 : IEventConsumer<TestEvent1>
        {
            public Task ConsumeAsync(EventContext<TestEvent1> context, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        [ConsumerName("sample-consumer")]
        class TestConsumer2 : IEventConsumer<TestEvent2>
        {
            public Task ConsumeAsync(EventContext<TestEvent2> context, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        class DummyEnvironment : IHostEnvironment
        {
            public DummyEnvironment(string applicationName) => ApplicationName = applicationName;

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }
        }
    }
}
