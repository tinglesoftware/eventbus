using System;

namespace Tingle.EventBus
{
    /// <summary>
    /// Specify the readiness provider type used for an event consumer event, overriding the default one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ConsumerReadinessProviderAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="readinessProviderType">
        /// The type of readiness provider to use for the consumer.
        /// It must implement <see cref="Readiness.IReadinessProvider"/>.
        /// </param>
        public ConsumerReadinessProviderAttribute(Type readinessProviderType)
        {
            ReadinessProviderType = readinessProviderType ?? throw new ArgumentNullException(nameof(readinessProviderType));

            // do not check if it implements IReadinessProvider here, it shall be done in the validation of options
        }

        /// <summary>
        /// The type of readiness provider to be used.
        /// </summary>
        public Type ReadinessProviderType { get; }
    }
}
