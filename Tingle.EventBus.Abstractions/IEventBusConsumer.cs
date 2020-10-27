using System;
using System.Collections.Generic;
using System.Text;

namespace Tingle.EventBus.Abstractions
{
    public interface IEventBusConsumer
    {
    }

    public interface IEventBusConsumer<T> : IEventBusConsumer
    {

    }
}
