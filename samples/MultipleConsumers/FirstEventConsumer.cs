using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace MultipleConsumers
{
    public class FirstEventConsumer : IEventConsumer<DoorOpened>
    {
        private readonly ILogger logger;

        public FirstEventConsumer(ILogger<FirstEventConsumer> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ConsumeAsync(EventContext<DoorOpened> context, CancellationToken cancellationToken = default)
        {
            var evt = context.Event;
            var vehicleId = evt.VehicleId;
            var kind = evt.Kind;
            logger.LogInformation("{DoorKind} door for {VehicleId} was opened at {Opened:r}.", kind, vehicleId, evt.Opened);
            return Task.CompletedTask;
        }
    }
}
