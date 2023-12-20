using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tingle.EventBus.Internal;

/// <summary>Host for <see cref="EventBus"/>.</summary>
/// <param name="lifetime"></param>
/// <param name="bus"></param>
/// <param name="logger"></param>
internal class EventBusHost(IHostApplicationLifetime lifetime, EventBus bus, ILogger<EventBusHost> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await WaitForAppStartupAsync(lifetime, stoppingToken).ConfigureAwait(false))
        {
            logger.ApplicationDidNotStartup();
            return;
        }

        await bus.StartAsync(stoppingToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await bus.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> WaitForAppStartupAsync(IHostApplicationLifetime lifetime, CancellationToken stoppingToken)
    {
        var startedTcs = new TaskCompletionSource<object>();
        var cancelledTcs = new TaskCompletionSource<object>();

        // register result setting using the cancellation tokens
        lifetime.ApplicationStarted.Register(() => startedTcs.SetResult(new { }));
        stoppingToken.Register(() => cancelledTcs.SetResult(new { }));

        var completedTask = await Task.WhenAny(startedTcs.Task, cancelledTcs.Task).ConfigureAwait(false);

        // if the completed task was the "app started" one, return true
        return completedTask == startedTcs.Task;
    }
}
