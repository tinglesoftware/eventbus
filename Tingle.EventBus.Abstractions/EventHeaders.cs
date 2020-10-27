namespace Tingle.EventBus.Abstractions
{
    public class EventHeaders
    {
        public string MessageId { get; set; }
        public string CorrelationId { get; set; }
    }
}
