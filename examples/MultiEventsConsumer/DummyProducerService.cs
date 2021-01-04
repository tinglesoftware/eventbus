using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus;

namespace MultiEventsConsumer
{
    internal class DummyProducerService : BackgroundService
    {
        private readonly IEventPublisher publisher;
        private readonly ILogger logger;

        public DummyProducerService(IEventPublisher publisher, ILogger<DummyProducerService> logger)
        {
            this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for bus to be ready
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

            // Generate random vehicle Ids
            var rnd = new Random(DateTimeOffset.UtcNow.Millisecond);
            var vehicles = Enumerable.Range(0, 5).Select(_ => GenerateRandomString(rnd)).ToList();

            // Find the number of possible combinations
            var kinds = Enum.GetValues<DoorKind>().ToList();
            var states = Enum.GetValues<DoorState>().ToList();
            var combinations = vehicles.Count * kinds.Count * states.Count;

            // Select a portion of the combinations
            var times = rnd.Next(1, combinations);
            logger.LogInformation("Selected {Times} of {Combinations} combinations.", times, combinations);

            var delay = TimeSpan.FromSeconds(15);

            for (var i = 0; i < times; i++)
            {
                // Select vehicle, door kind and door state randonly
                var vehicle = vehicles[rnd.Next(0, combinations) * 1 % vehicles.Count];
                var kind = kinds[rnd.Next(0, combinations) * i % kinds.Count];
                var state = states[rnd.Next(0, combinations) * i % states.Count];

                // Publish event depending on the door state
                if (state == DoorState.Closed)
                {
                    var evt = new DoorClosed
                    {
                        VehicleId = vehicle,
                        Closed = DateTimeOffset.UtcNow,
                        Kind = kind,
                    };

                    await publisher.PublishAsync(evt, cancellationToken: stoppingToken);
                }
                else
                {
                    var evt = new DoorOpened
                    {
                        VehicleId = vehicle,
                        Opened = DateTimeOffset.UtcNow,
                        Kind = kind,
                    };

                    await publisher.PublishAsync(evt, cancellationToken: stoppingToken);
                }

                await Task.Delay(delay, stoppingToken);
            }

            logger.LogInformation("Finished producing dummy data!");
        }

        private static string GenerateRandomString(Random random)
        {
            var bys = new byte[20];
            random.NextBytes(bys);
            var result = Convert.ToBase64String(bys);
            return System.Text.RegularExpressions.Regex.Replace(result, "[^A-Za-z0-9]", "");
        }
    }
}