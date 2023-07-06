using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Tests.Configurator;

public class DefaultEventConfiguratorTests
{
    [Fact]
    public void ConfigureSerializer_UsesDefault()
    {
        var configuration = new ConfigurationBuilder().Build();
        var configurationProvider = new DefaultEventBusConfigurationProvider(configuration);
        var configurator = new DefaultEventConfigurator(new FakeHostEnvironment("app1"), configurationProvider);

        // when not set, use default
        var registration = new EventRegistration(typeof(TestEvent1));
        Assert.Null(registration.EventSerializerType);
        configurator.ConfigureSerializer(registration);
        Assert.Equal(typeof(IEventSerializer), registration.EventSerializerType);
    }

    [Fact]
    public void ConfigureSerializer_RespectsAttribute()
    {
        var configuration = new ConfigurationBuilder().Build();
        var configurationProvider = new DefaultEventBusConfigurationProvider(configuration);
        var configurator = new DefaultEventConfigurator(new FakeHostEnvironment("app1"), configurationProvider);

        // attribute is respected
        var registration = new EventRegistration(typeof(TestEvent2));
        Assert.Null(registration.EventSerializerType);
        configurator.ConfigureSerializer(registration);
        Assert.Equal(typeof(FakeEventSerializer1), registration.EventSerializerType);
    }

    [Fact]
    public void ConfigureSerializer_Throws_InvalidOperationException()
    {
        var configuration = new ConfigurationBuilder().Build();
        var configurationProvider = new DefaultEventBusConfigurationProvider(configuration);
        var configurator = new DefaultEventConfigurator(new FakeHostEnvironment("app1"), configurationProvider);

        // attribute is respected
        var registration = new EventRegistration(typeof(TestEvent3));
        var ex = Assert.Throws<InvalidOperationException>(() => configurator.ConfigureSerializer(registration));
        Assert.Equal("The type 'Tingle.EventBus.Tests.Configurator.FakeEventSerializer2' is used"
                   + " as a serializer but does not implement 'Tingle.EventBus.Serialization.IEventSerializer'",
            ex.Message);
    }

    [Theory]
    [InlineData(typeof(TestEvent1), false, "dev", NamingConvention.KebabCase, "dev-test-event1")]
    [InlineData(typeof(TestEvent1), false, "dev", NamingConvention.SnakeCase, "dev_test_event1")]
    [InlineData(typeof(TestEvent1), false, "dev", NamingConvention.DotCase, "dev.test.event1")]
    [InlineData(typeof(TestEvent1), true, "dev", NamingConvention.KebabCase, "dev-tingle-event-bus-tests-configurator-test-event1")]
    [InlineData(typeof(TestEvent1), true, "dev", NamingConvention.SnakeCase, "dev_tingle_event_bus_tests_configurator_test_event1")]
    [InlineData(typeof(TestEvent1), true, "dev", NamingConvention.DotCase, "dev.tingle.event.bus.tests.configurator.test.event1")]
    // Overriden by attribute
    [InlineData(typeof(TestEvent2), true, "dev", NamingConvention.KebabCase, "sample-event")]
    [InlineData(typeof(TestEvent2), true, "dev", NamingConvention.SnakeCase, "sample-event")]
    [InlineData(typeof(TestEvent2), true, "dev", NamingConvention.DotCase, "sample-event")]
    public void ConfigureEventName_Works(Type eventType, bool useFullTypeNames, string scope, NamingConvention namingConvention, string expected)
    {
        var configuration = new ConfigurationBuilder().Build();
        var configurationProvider = new DefaultEventBusConfigurationProvider(configuration);
        var configurator = new DefaultEventConfigurator(new FakeHostEnvironment("app1"), configurationProvider);

        var options = new EventBusOptions { };
        options.Naming.Scope = scope;
        options.Naming.Convention = namingConvention;
        options.Naming.UseFullTypeNames = useFullTypeNames;
        var registration = new EventRegistration(eventType);
        configurator.ConfigureEventName(registration, options.Naming);
        Assert.Equal(expected, registration.EventName);
    }

    [Theory]
    // Full type names
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.TypeName, NamingConvention.KebabCase, "test-consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.TypeName, NamingConvention.SnakeCase, "test_consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.TypeName, NamingConvention.DotCase, "test.consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, null, ConsumerNameSource.Prefix, NamingConvention.KebabCase, "app1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, null, ConsumerNameSource.Prefix, NamingConvention.SnakeCase, "app1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, null, ConsumerNameSource.Prefix, NamingConvention.DotCase, "app1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.KebabCase, "app1-test-consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.SnakeCase, "app1_test_consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.DotCase, "app1.test.consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.Prefix, NamingConvention.KebabCase, "service1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.Prefix, NamingConvention.SnakeCase, "service1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.Prefix, NamingConvention.DotCase, "service1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.KebabCase, "service1-test-consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.SnakeCase, "service1_test_consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), false, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.DotCase, "service1.test.consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, "service1", ConsumerNameSource.TypeName, NamingConvention.KebabCase,
        "tingle-event-bus-tests-configurator-test-consumer1")]

    // Short type names
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.TypeName, NamingConvention.KebabCase,
        "tingle-event-bus-tests-configurator-test-consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.TypeName, NamingConvention.SnakeCase,
        "tingle_event_bus_tests_configurator_test_consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.TypeName, NamingConvention.DotCase,
        "tingle.event.bus.tests.configurator.test.consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.Prefix, NamingConvention.KebabCase, "app1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.Prefix, NamingConvention.SnakeCase, "app1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.Prefix, NamingConvention.DotCase, "app1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.KebabCase,
        "app1-tingle-event-bus-tests-configurator-test-consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.SnakeCase,
        "app1_tingle_event_bus_tests_configurator_test_consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.DotCase,
        "app1.tingle.event.bus.tests.configurator.test.consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, "service1", ConsumerNameSource.Prefix, NamingConvention.KebabCase, "service1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, "service1", ConsumerNameSource.Prefix, NamingConvention.SnakeCase, "service1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, "service1", ConsumerNameSource.Prefix, NamingConvention.DotCase, "service1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.KebabCase,
        "service1-tingle-event-bus-tests-configurator-test-consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.SnakeCase,
        "service1_tingle_event_bus_tests_configurator_test_consumer1")]
    [InlineData(typeof(TestEvent1), typeof(TestConsumer1), true, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.DotCase,
        "service1.tingle.event.bus.tests.configurator.test.consumer1")]

    // Overriden by attribute
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), false, null, ConsumerNameSource.TypeName, NamingConvention.SnakeCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), false, null, ConsumerNameSource.Prefix, NamingConvention.SnakeCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), false, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.SnakeCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), false, "service1", ConsumerNameSource.PrefixAndTypeName, NamingConvention.DotCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, "service1", ConsumerNameSource.TypeName, NamingConvention.KebabCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, "service1", ConsumerNameSource.Prefix, NamingConvention.KebabCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, "service1", ConsumerNameSource.Prefix, NamingConvention.DotCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.KebabCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.SnakeCase, "sample-consumer")]
    [InlineData(typeof(TestEvent2), typeof(TestConsumer2), true, null, ConsumerNameSource.PrefixAndTypeName, NamingConvention.DotCase, "sample-consumer")]
    public void SetConsumerName_Works(Type eventType,
                                      Type consumerType,
                                      bool useFullTypeNames,
                                      string prefix,
                                      ConsumerNameSource consumerNameSource,
                                      NamingConvention namingConvention,
                                      string expected)
    {
        var configuration = new ConfigurationBuilder().Build();
        var configurationProvider = new DefaultEventBusConfigurationProvider(configuration);
        var configurator = new DefaultEventConfigurator(new FakeHostEnvironment("app1"), configurationProvider);

        var options = new EventBusOptions { };
        options.Naming.Convention = namingConvention;
        options.Naming.UseFullTypeNames = useFullTypeNames;
        options.Naming.ConsumerNameSource = consumerNameSource;
        options.Naming.ConsumerNamePrefix = prefix;

        var registration = new EventRegistration(eventType);
        registration.Consumers.Add(new EventConsumerRegistration(consumerType, false));

        var creg = Assert.Single(registration.Consumers);
        configurator.ConfigureEventName(registration, options.Naming);
        configurator.ConfigureConsumerNames(registration, options.Naming);
        Assert.Equal(expected, creg.ConsumerName);
    }

    [Theory]
    [InlineData(typeof(TestEvent1), null)]
    [InlineData(typeof(TestEvent2), null)]
    [InlineData(typeof(TestEvent3), EntityKind.Broadcast)]
    public void ConfigureEntityKind_Works(Type eventType, EntityKind? expected)
    {
        var configuration = new ConfigurationBuilder().Build();
        var configurationProvider = new DefaultEventBusConfigurationProvider(configuration);
        var configurator = new DefaultEventConfigurator(new FakeHostEnvironment("app1"), configurationProvider);

        var registration = new EventRegistration(eventType);
        configurator.ConfigureEntityKind(registration);
        Assert.Equal(expected, registration.EntityKind);
    }
}
