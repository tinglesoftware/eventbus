using System.Threading.Channels;
using System.Collections.Concurrent;

namespace Tingle.EventBus.Transports.InMemory
{
    internal class InMemoryClient
    {
        private readonly ConcurrentDictionary<string, Channel<InMemoryMessage>> channels = new();

        /// <summary>
        /// Creates an <see cref="InMemoryProcessor"/> instance that can be used to process messages
        /// using event handlers that are set on the processor.
        /// </summary>
        /// <param name="queueName">The queue to create an <see cref="InMemoryProcessor"/> for.</param>
        /// <param name="options">
        /// The set of <see cref="InMemoryProcessorOptions"/> to use for configuring
        /// the <see cref="InMemoryProcessor"/>.
        /// </param>
        /// <returns>An <see cref="InMemoryProcessor"/> scoped to the specified queue.</returns>
        public virtual InMemoryProcessor CreateProcessor(string queueName, InMemoryProcessorOptions options)
        {
            var channel = GetChannel(queueName);
            return new InMemoryProcessor(channel.Reader);
        }

        /// <summary>
        /// Creates and <see cref="InMemoryProcessor"/> instance that can be used to process messages
        /// using event handlers that are set on the processor.
        /// </summary>
        /// <param name="topicName">The topic to create an <see cref="InMemoryProcessor"/> for.</param>
        /// <param name="subscriptionName">The subscription to create an <see cref="InMemoryProcessor"/> for.</param>
        /// <param name="options">
        /// The set of <see cref="InMemoryProcessorOptions"/> to use for configuring
        /// the <see cref="InMemoryProcessor"/>.
        /// </param>
        /// <returns>An <see cref="InMemoryProcessor"/> scoped to the specified topic and subscription.</returns>
        public virtual InMemoryProcessor CreateProcessor(string topicName, string subscriptionName, InMemoryProcessorOptions options)
        {
            var channel = GetChannel(topicName);
            return new InMemoryProcessor(channel.Reader);
        }

        /// <summary>
        /// Creates an <see cref="InMemorySender"/> instance that can be used to sending messages
        /// to a specific queue or topic.
        /// </summary>
        /// <param name="queueOrTopicName">The queue or topic to create a Azure.Messaging.ServiceBus.ServiceBusSender for.</param>
        /// <returns>An <see cref="InMemorySender"/> scoped to the specified queue or topic.</returns>
        public virtual InMemorySender CreateSender(string queueOrTopicName)
        {
            var channel = GetChannel(queueOrTopicName);
            return new InMemorySender(channel.Writer);
        }

        private Channel<InMemoryMessage> GetChannel(string queueOrTopicName)
        {
            // TODO: pass options to the channel
            return channels.GetOrAdd(queueOrTopicName, (name) => Channel.CreateUnbounded<InMemoryMessage>());
        }
    }
}
