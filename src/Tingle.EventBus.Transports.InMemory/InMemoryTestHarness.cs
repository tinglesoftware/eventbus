using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Tingle.EventBus.Transports.InMemory;

///
public class InMemoryTestHarness(EventBusTransportProvider transportProvider, IOptions<InMemoryTestHarnessOptions> optionsAccessor)
{
    private readonly InMemoryTransport transport = transportProvider.GetTransport(InMemoryDefaults.Name) as InMemoryTransport
            ?? throw new ArgumentException("The InMemoryTransport must be added. Ensure 'services.AddInMemoryTransport()' has been called.");
    private readonly InMemoryTestHarnessOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

    ///
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await transport.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    ///
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await transport.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all the published events.
    /// </summary>
    public IEnumerable<EventContext> Published() => transport.Published;

    /// <summary>
    /// Gets all the published events.
    /// </summary>
    /// <param name="delay">
    /// The duration of time to delay.
    /// When <see langword="null"/>, the default value (<see cref="InMemoryTestHarnessOptions.DefaultDelay"/>) is used
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<EventContext>> PublishedAsync(TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay ?? options.DefaultDelay, cancellationToken).ConfigureAwait(false);
        return Published();
    }

    /// <summary>
    /// Gets all the published events of a given type.
    /// <typeparam name="T">The type of event carried.</typeparam>
    /// </summary>
    public IEnumerable<EventContext<T>> Published<T>() where T : class => transport.Published.OfType<EventContext<T>>();

    /// <summary>
    /// Gets all the published events of a given type.
    /// </summary>
    /// <typeparam name="T">The type of event carried.</typeparam>
    /// <param name="delay">
    /// The duration of time to delay.
    /// When <see langword="null"/>, the default value (<see cref="InMemoryTestHarnessOptions.DefaultDelay"/>) is used
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<EventContext<T>>> PublishedAsync<T>(TimeSpan? delay = null, CancellationToken cancellationToken = default)
        where T : class
    {
        await Task.Delay(delay ?? options.DefaultDelay, cancellationToken).ConfigureAwait(false);
        return Published<T>();
    }

    /// <summary>
    /// Gets all the cancelled events.
    /// </summary>
    public IEnumerable<long> Cancelled() => transport.Cancelled;

    /// <summary>
    /// Gets all the cancelled events.
    /// </summary>
    /// <param name="delay">
    /// The duration of time to delay.
    /// When <see langword="null"/>, the default value (<see cref="InMemoryTestHarnessOptions.DefaultDelay"/>) is used
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<long>> CancelledAsync(TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay ?? options.DefaultDelay, cancellationToken).ConfigureAwait(false);
        return Cancelled();
    }

    /// <summary>
    /// Gets all the consumed events.
    /// </summary>
    public IEnumerable<EventContext> Consumed() => transport.Consumed;

    /// <summary>
    /// Gets all the consumed events.
    /// </summary>
    /// <param name="delay">
    /// The duration of time to delay.
    /// When <see langword="null"/>, the default value (<see cref="InMemoryTestHarnessOptions.DefaultDelay"/>) is used
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<EventContext>> ConsumedAsync(TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay ?? options.DefaultDelay, cancellationToken).ConfigureAwait(false);
        return Consumed();
    }

    /// <summary>
    /// Gets all the consumed events of a given type.
    /// <typeparam name="T">The type of event carried.</typeparam>
    /// </summary>
    public IEnumerable<EventContext<T>> Consumed<T>() where T : class => transport.Consumed.OfType<EventContext<T>>();

    /// <summary>
    /// Get all the consumed events of a given type.
    /// </summary>
    /// <typeparam name="T">The type of event carried.</typeparam>
    /// <param name="delay">
    /// The duration of time to delay.
    /// When <see langword="null"/>, the default value (<see cref="InMemoryTestHarnessOptions.DefaultDelay"/>) is used
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<EventContext<T>>> ConsumedAsync<T>(TimeSpan? delay = null, CancellationToken cancellationToken = default)
        where T : class
    {
        await Task.Delay(delay ?? options.DefaultDelay, cancellationToken).ConfigureAwait(false);
        return Consumed<T>();
    }

    /// <summary>
    /// Gets all the failed events.
    /// </summary>
    public IEnumerable<EventContext> Failed() => transport.Failed;

    /// <summary>
    /// Gets all the failed events of a given type.
    /// </summary>
    /// <param name="delay">
    /// The duration of time to delay.
    /// When <see langword="null"/>, the default value (<see cref="InMemoryTestHarnessOptions.DefaultDelay"/>) is used
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<EventContext>> FailedAsync(TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delay ?? options.DefaultDelay, cancellationToken).ConfigureAwait(false);
        return Failed();
    }

    /// <summary>
    /// Gets all the failed events of a given type.
    /// </summary>
    public IEnumerable<EventContext<T>> Failed<T>() where T : class => transport.Failed.OfType<EventContext<T>>();

    /// <summary>
    /// Gets all the failed events of a given type.
    /// </summary>
    /// <typeparam name="T">The type of event carried.</typeparam>
    /// <param name="delay">
    /// The duration of time to delay.
    /// When <see langword="null"/>, the default value (<see cref="InMemoryTestHarnessOptions.DefaultDelay"/>) is used
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<EventContext<T>>> FailedAsync<T>(TimeSpan? delay = null, CancellationToken cancellationToken = default)
        where T : class
    {
        await Task.Delay(delay ?? options.DefaultDelay, cancellationToken).ConfigureAwait(false);
        return Failed<T>();
    }
}
