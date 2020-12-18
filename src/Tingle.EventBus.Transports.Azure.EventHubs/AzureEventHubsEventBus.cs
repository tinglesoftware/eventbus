using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.Azure.EventHubs
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using Azure Event Hubs.
    /// </summary>
    public class AzureEventHubsEventBus : EventBusBase<AzureEventHubsOptions>
    {
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureEventHubsEventBus(IHostEnvironment environment,
                                      IServiceScopeFactory serviceScopeFactory,
                                      IOptions<EventBusOptions> busOptionsAccessor,
                                      IOptions<AzureEventHubsOptions> transportOptionsAccessor,
                                      ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            logger = loggerFactory?.CreateLogger<AzureEventHubsEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public override Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
