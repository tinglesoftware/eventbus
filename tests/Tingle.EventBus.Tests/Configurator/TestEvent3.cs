namespace Tingle.EventBus.Tests.Configurator
{
    [EntityKind(EntityKind.Broadcast)]
    [EventSerializer(typeof(FakeEventSerializer2))]
    internal class TestEvent3
    {
        public string? Value1 { get; set; }
        public string? Value2 { get; set; }
    }
}
