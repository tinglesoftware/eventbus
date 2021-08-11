﻿using Microsoft.Extensions.DependencyInjection;
using Tingle.EventBus.Registrations;

namespace CustomEventConfigurator
{
    class MyConfigurator : IEventConfigurator
    {
        public void Configure(EventRegistration registration, EventBusOptions options)
        {
            if (registration.EventType == typeof(SampleEvent1))
            {
                registration.EntityKind = Tingle.EventBus.EntityKind.Queue;
            }

            if (registration.EventType == typeof(SampleEvent2))
            {
                registration.IdFormat = Tingle.EventBus.EventIdFormat.LongHex;
            }
        }
    }
}