using Tingle.EventBus.Configuration;
using Tingle.EventBus.Transports;

namespace CustomEventConfigurator;

class MyConfigurator : IEventBusConfigurator
{
    public void Configure(EventBusOptions options) { }

    public void Configure<TOptions>(IConfiguration configuration, TOptions options) where TOptions : EventBusTransportOptions { }

    public void Configure(EventRegistration registration, EventBusOptions options)
    {
        if (registration.EventType == typeof(SampleEvent1))
        {
            registration.EntityKind = EntityKind.Queue;
        }

        if (registration.EventType == typeof(SampleEvent2))
        {
            registration.IdFormat = EventIdFormat.LongHex;
        }
    }
}
