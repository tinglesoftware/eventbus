namespace Tingle.EventBus.Transports
{
    internal interface IEventBusTransportWithOptions
    {
        EventBusTransportOptionsBase GetOptions();
    }
}
