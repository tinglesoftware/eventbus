using Microsoft.Extensions.Caching.Distributed;

namespace MultiEventsConsumer;

public class MultiEventsConsumer : IEventConsumer<DoorClosed>, IEventConsumer<DoorOpened>
{
    private static readonly TimeSpan SimulationDuration = TimeSpan.FromSeconds(3);

    private readonly IDistributedCache cache;
    private readonly ILogger logger;

    public MultiEventsConsumer(IDistributedCache cache, ILogger<MultiEventsConsumer> logger)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync(EventContext<DoorOpened> context, CancellationToken cancellationToken = default)
    {
        var evt = context.Event;
        var vehicleId = evt.VehicleId;
        var kind = evt.Kind;
        logger.LogInformation("{DoorKind} door for {VehicleId} was opened at {Opened:r}.", kind, vehicleId, evt.Opened);

        // Get the door state from the cache (or database)
        var state = await GetLastDoorStateAsync(vehicleId, kind, cancellationToken);

        // If the door is not currently open, update and send a notification
        if (state != DoorState.Open)
        {
            // save to cache (or database)
            await SaveDoorStateAsync(vehicleId, kind, DoorState.Open, cancellationToken);

            // Send push notification
            logger.LogInformation("Sending push notification.");
            await Task.Delay(SimulationDuration, cancellationToken); // simulate using delay
        }
        else
        {
            logger.LogInformation("Ignoring door opened event because the door was already opened");
        }
    }

    public async Task ConsumeAsync(EventContext<DoorClosed> context, CancellationToken cancellationToken = default)
    {
        var evt = context.Event;
        var vehicleId = evt.VehicleId;
        var kind = evt.Kind;
        logger.LogInformation("{DoorKind} door for {VehicleId} was closed at {Opened:r}.", kind, vehicleId, evt.Closed);

        // Get the door state from the cache (or database)
        var state = await GetLastDoorStateAsync(vehicleId, kind, cancellationToken);

        // If the door is not currently closed, update and send a notification
        if (state != DoorState.Closed)
        {
            // save to cache (or database)
            await SaveDoorStateAsync(vehicleId, kind, DoorState.Closed, cancellationToken);

            // Send push notification
            logger.LogInformation("Sending push notification.");
            await Task.Delay(SimulationDuration, cancellationToken); // simulate using delay
        }
        else
        {
            logger.LogInformation("Ignoring door closed event because the door was already closed");
        }
    }

    private static string MakeCacheKey(string vehicleId, DoorKind kind) => $"{vehicleId}/{kind}".ToLowerInvariant();

    private async Task<DoorState?> GetLastDoorStateAsync(string? vehicleId, DoorKind kind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            throw new ArgumentException($"'{nameof(vehicleId)}' cannot be null or whitespace.", nameof(vehicleId));
        }

        string key = MakeCacheKey(vehicleId, kind);
        var stateStr = await cache.GetStringAsync(key, cancellationToken);
        return Enum.TryParse<DoorState>(stateStr, ignoreCase: true, out var state) ? (DoorState?)state : null;
    }

    private async Task SaveDoorStateAsync(string? vehicleId, DoorKind kind, DoorState state, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            throw new ArgumentException($"'{nameof(vehicleId)}' cannot be null or whitespace.", nameof(vehicleId));
        }

        string key = MakeCacheKey(vehicleId, kind);
        var stateStr = state.ToString().ToLowerInvariant();
        await cache.SetStringAsync(key, stateStr, cancellation);
    }
}
