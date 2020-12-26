using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;

namespace Tingle.EventBus.Transports.Amazon.Kinesis
{
    /// <summary>
    /// Implementation of <see cref="IEventBus"/> via <see cref="EventBusBase{TTransportOptions}"/> using
    /// Amazon Kinesis as the transport.
    /// </summary>
    public class AmazonKinesisEventBus : EventBusBase<AmazonKinesisOptions>
    {
        private readonly AmazonKinesisClient kinesisClient;
        private readonly ILogger logger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="busOptionsAccessor"></param>
        /// <param name="transportOptionsAccessor"></param>
        /// <param name="loggerFactory"></param>
        public AmazonKinesisEventBus(IHostEnvironment environment,
                                     IServiceScopeFactory serviceScopeFactory,
                                     IOptions<EventBusOptions> busOptionsAccessor,
                                     IOptions<AmazonKinesisOptions> transportOptionsAccessor,
                                     ILoggerFactory loggerFactory)
            : base(environment, serviceScopeFactory, busOptionsAccessor, transportOptionsAccessor, loggerFactory)
        {
            kinesisClient = new AmazonKinesisClient(credentials: TransportOptions.Credentials,
                                                    clientConfig: TransportOptions.KinesisConfig);

            logger = loggerFactory?.CreateLogger<AmazonKinesisEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc/>
        public override async Task<bool> CheckHealthAsync(EventBusHealthCheckExtras extras,
                                                          CancellationToken cancellationToken = default)
        {
            _ = await kinesisClient.ListStreamsAsync(cancellationToken);
            return true;
        }

        /// <inheritdoc/>
        protected override Task StartBusAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override Task StopBusAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override Task<string> PublishOnBusAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override Task<IList<string>> PublishOnBusAsync<TEvent>(IList<EventContext<TEvent>> events, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(string id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task CancelAsync<TEvent>(IList<string> ids, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
