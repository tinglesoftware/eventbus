using Microsoft.Extensions.DependencyInjection;
using Tingle.EventBus.Configuration;

namespace CustomEventConfigurator
{
    class MyConfigurator : IEventConfigurator
    {
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
}
