using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Transports.Azure.QueueStorage
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using Azure Queue Storage.
    /// </summary>
    public class AzureQueueStorageEventBus : EventBusBase<AzureQueueStorageOptions>
    {
        //private readonly QueueClient queueClient;
        private readonly QueueServiceClient serviceClient;
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AzureQueueStorageEventBus(IHostEnvironment environment,
                                         IServiceScopeFactory serviceScopeFactory,
                                         IOptions<EventBusOptions> busOptionsAccessor,
                                         IOptions<AzureQueueStorageOptions> transportOptionsAccessor,
                                         ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            serviceClient = new QueueServiceClient(TransportOptions.ConnectionString);
            logger = loggerFactory?.CreateLogger<AzureQueueStorageEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            await serviceClient.GetStatisticsAsync(cancellationToken);
            return true;
        }

        /// <inheritdoc/>
        public override Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<IList<string>> PublishAsync<TEvent>(IList<EventContext<TEvent>> events, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
