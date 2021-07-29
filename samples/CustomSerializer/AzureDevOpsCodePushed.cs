using CustomSerializer.Models;
using Tingle.EventBus;

namespace CustomSerializer
{
    [EventSerializer(typeof(AzureDevOpsEventSerializer))]
    public sealed class AzureDevOpsCodePushed
    {
        public AzureDevOpsEventResource? Resource { get; set; }
    }
}
