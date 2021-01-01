using System;

namespace Tingle.EventBus.Transports
{
    /// <summary>
    /// The base options for an event bus transport
    /// </summary>
    public abstract class EventBusTransportOptionsBase
    {
        /// <summary>
        /// The delay to introduce everytime zero messages are received.
        /// This eases on the CPU consumption and reduces the query costs.
        /// The default value is 1 minute. Max value is 10 minutes and minimum is 30 seconds.
        /// </summary>
        public TimeSpan EmptyResultsDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets value indicating if the transport's entities
        /// (e.g. queues, topics, subscriptions, exchanges) should be created.
        /// If <see langword="false"/>, it is the responsibility of the developer to create
        /// entities.
        /// Always set this value to <see langword="false"/> when the credentials
        /// or connection string in use lack the requisite permissions for creation.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool EnableEntityCreation { get; set; } = true;
    }
}
