using System;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure Queue Storage based event bus.
    /// </summary>
    public class AzureQueueStorageTransportOptions : EventBusTransportOptionsBase
    {
        /// <summary>
        /// The connection string to Azure Queue Storage.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The delay to introduce everytime zero messages are received.
        /// This eases on the CPU consumption and reduces the query costs.
        /// This value must be between 30 seconds and 10 minutes.
        /// Defaults to 1 minute.
        /// </summary>
        public TimeSpan EmptyResultsDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets value indicating if the queues should be created.
        /// If <see langword="false"/>, it is the responsibility of the
        /// developer to create queues.
        /// Always set this value to <see langword="false"/> when the <see cref="ConnectionString"/>
        /// is a shared access signature without the <c>Manage</c> permission.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool EnableQueueCreation { get; set; } = true;
    }
}
