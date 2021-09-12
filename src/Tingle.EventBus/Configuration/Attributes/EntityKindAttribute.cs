using System;

namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// Specify the EntityKind used for this event contract/type, overriding the generated one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class EntityKindAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="kind">The event kind to use for the event.</param>
        public EntityKindAttribute(EntityKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// The name of the event mapped
        /// </summary>
        public EntityKind Kind { get; }
    }
}
