using Azure.Messaging.EventHubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureIotHub;

internal class AzureIotEventsConsumer : IEventConsumer<EventData>
{
    public Task ConsumeAsync(EventContext<EventData> context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
