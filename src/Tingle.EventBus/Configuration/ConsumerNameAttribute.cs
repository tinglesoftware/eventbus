using System;

namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// Specify the ConsumerName used for this consumer type, overriding the generated one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ConsumerNameAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="consumerName">The consumer name to use for the consumer type</param>
        public ConsumerNameAttribute(string consumerName)
        {
            if (string.IsNullOrWhiteSpace(consumerName))
            {
                throw new ArgumentException($"'{nameof(consumerName)}' cannot be null or whitespace", nameof(consumerName));
            }

            ConsumerName = consumerName;
        }

        /// <summary>
        /// The name of the consumer
        /// </summary>
        public string ConsumerName { get; }
    }
}
