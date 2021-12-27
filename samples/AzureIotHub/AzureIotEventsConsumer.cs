using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureIotHub;

internal class AzureIotEventsConsumer : IEventConsumer<MyIotHubEvent>
{
    public Task ConsumeAsync(EventContext<MyIotHubEvent> context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
