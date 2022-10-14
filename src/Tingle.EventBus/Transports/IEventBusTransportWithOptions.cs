namespace Tingle.EventBus.Transports;

internal interface IEventBusTransportWithOptions
{
    EventBusTransportOptions GetOptions();
}
