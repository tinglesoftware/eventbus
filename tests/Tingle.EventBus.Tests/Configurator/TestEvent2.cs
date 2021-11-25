using Tingle.EventBus.Configuration;

namespace Tingle.EventBus.Tests.Configurator;

[EventName("sample-event")]
[EventSerializer(typeof(FakeEventSerializer1))]
internal class TestEvent2
{
    public string? Value1 { get; set; }
    public string? Value2 { get; set; }
}
