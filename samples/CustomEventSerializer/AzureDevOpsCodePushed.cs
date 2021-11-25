using CustomEventSerializer.Models;
using Tingle.EventBus.Configuration;

namespace CustomEventSerializer;

[EventSerializer(typeof(AzureDevOpsEventSerializer))]
public sealed class AzureDevOpsCodePushed
{
    public AzureDevOpsEventResource? Resource { get; set; }
}
