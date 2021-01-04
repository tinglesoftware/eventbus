using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.InMemory
{
    ///
    public class InMemoryTestHarness
    {
        private readonly InMemoryTransport transport;

        ///
        public InMemoryTestHarness(IEnumerable<IEventBusTransport> transports)
        {
            if (transports is null) throw new ArgumentNullException(nameof(transports));

            transport = transports.OfType<InMemoryTransport>().SingleOrDefault();
            if (transport == null)
            {
                throw new ArgumentException("The InMemoryTransport must be added. Ensure 'services.AddInMemoryTransport()' has been called.", nameof(transport));
            }
        }

        ///
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await transport.StartAsync(cancellationToken);
        }

        ///
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await transport.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Gets all the published events.
        /// </summary>
        public IEnumerable<EventContext> Published() => transport.Published;

        /// <summary>
        /// Gets all the published events of a given type.
        /// </summary>
        public IEnumerable<EventContext<T>> Published<T>() => transport.Published.OfType<EventContext<T>>();

        /// <summary>
        /// Gets all the consumed events.
        /// </summary>
        public IEnumerable<EventContext> Consumed() => transport.Consumed;

        /// <summary>
        /// Gets all the consumed events of a given type.
        /// </summary>
        public IEnumerable<EventContext<T>> Consumed<T>() => transport.Consumed.OfType<EventContext<T>>();

        /// <summary>
        /// Gets all the failed events.
        /// </summary>
        public IEnumerable<EventContext> Failed() => transport.Failed;

        /// <summary>
        /// Gets all the failed events of a given type.
        /// </summary>
        public IEnumerable<EventContext<T>> Failed<T>() => transport.Failed.OfType<EventContext<T>>();
    }
}
