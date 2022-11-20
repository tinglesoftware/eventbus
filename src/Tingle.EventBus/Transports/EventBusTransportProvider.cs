using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Tingle.EventBus.Transports;

///
public sealed class EventBusTransportProvider
{
    private readonly IServiceProvider serviceProvider;
    private readonly EventBusOptions options;

    private readonly IReadOnlyDictionary<string, EventBusTransportRegistration> registrations;
    private readonly ConcurrentDictionary<string, IEventBusTransport> transports = new();

    /// 
    public EventBusTransportProvider(IServiceProvider serviceProvider, IOptions<EventBusOptions> optionsAccessor)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

        var registrations = new Dictionary<string, EventBusTransportRegistration>();
        foreach (var builder in options.Transports)
        {
            var tr = builder.Build();
            registrations.Add(tr.Name, tr);
        }
        this.registrations = registrations;
    }

    ///
    internal IReadOnlyDictionary<string, IEventBusTransport> GetTransports()
    {
        var registered = registrations.Keys;
        var materialized = transports.Keys;
        var pending = registered.Except(materialized).ToList();

        foreach (var name in pending) GetTransport(name);
        return new Dictionary<string, IEventBusTransport>(transports.ToArray());
    }

    ///
    public IEventBusTransport GetTransport(string name)
    {
        if (!registrations.TryGetValue(name, out var tr))
        {
            throw new InvalidOperationException($"The transport '{name}' is not registered. Ensure the transport is registered via builder.AddTransport(...)");
        }

        return transports.GetOrAdd(name, n =>
        {
            /*
             * Create the transport.
             * 
             * Do not resolve transports from services because multiple transports of the same type but different names maybe registered.
             * Resolving the same transport type would result in initialization the same instance, which would be erroneous when started.
             * Create multiple transports of the same type means each instance can listen to events/messages independently.
            */
            var transport = (IEventBusTransport)ActivatorUtilities.CreateInstance(serviceProvider, tr.TransportType);
            transport.Initialize(tr); // initialize the transport
            return transport;
        });
    }
}
