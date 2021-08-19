using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Tingle.EventBus
{
    /// <summary>
    /// Extension methods on <see cref="EventContext"/> and <see cref="EventContext{T}"/>.
    /// </summary>
    public static class EventContextExtensions
    {
        internal const string ItemsKeyConsumerGroup = "azure.eventhubs.consumer-group";
        internal const string ItemsKeyPartitionContext = "azure.eventhubs.partition-context";
        internal const string ItemsKeyEventData = "azure.eventhubs.event-data";

        /// <summary>
        /// Gets the ConsumerGroup associated with the specified <see cref="EventContext"/>
        /// if the event uses Azure Event Hubs transport.
        /// </summary>
        /// <param name="context">The <see cref="EventContext"/> to use.</param>
        /// <param name="consumerGroup">
        /// When this method returns, the value, if found; otherwise,
        /// the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// true if the ConsumerGroup is found; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">The context is null</exception>
        public static bool TryGetConsumerGroup(this EventContext context, [NotNullWhen(true)] out string? consumerGroup)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            if (context.Items.TryGetValue(ItemsKeyPartitionContext, out var obj) && obj is string cg)
            {
                consumerGroup = cg;
                return true;
            }

            consumerGroup = default;
            return false;
        }

        /// <summary>
        /// Gets the <see cref="PartitionContext"/> associated with the specified <see cref="EventContext"/>
        /// if the event uses Azure Event Hubs transport.
        /// </summary>
        /// <param name="context">The <see cref="EventContext"/> to use.</param>
        /// <param name="partition">
        /// When this method returns, the value, if found; otherwise,
        /// the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// true if the partition is found; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">The context is null</exception>
        public static bool TryGetPartitionContext(this EventContext context, [NotNullWhen(true)] out PartitionContext? partition)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            if (context.Items.TryGetValue(ItemsKeyPartitionContext, out var obj) && obj is PartitionContext pc)
            {
                partition = pc;
                return true;
            }

            partition = default;
            return false;
        }

        /// <summary>
        /// Gets the <see cref="EventData"/> associated with the specified <see cref="EventContext"/>
        /// if the event uses Azure Event Hubs transport.
        /// </summary>
        /// <param name="context">The <see cref="EventContext"/> to use.</param>
        /// <param name="data">
        /// When this method returns, the value, if found; otherwise,
        /// the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// true if the data is found; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">The context is null</exception>
        public static bool TryGetEventData(this EventContext context, [NotNullWhen(true)] out EventData? data)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            if (context.Items.TryGetValue(ItemsKeyEventData, out var obj) && obj is EventData d)
            {
                data = d;
                return true;
            }

            data = default;
            return false;
        }

        /// <summary>
        /// Set the ConsumerGroup for an event.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The <see cref="EventContext"/> to update.</param>
        /// <param name="consumerGroup">The value to set.</param>
        /// <returns>The updated context.</returns>
        internal static T SetConsumerGroup<T>(this T context, string consumerGroup) where T : EventContext
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (consumerGroup is null) throw new ArgumentNullException(nameof(consumerGroup));

            context.Items[ItemsKeyConsumerGroup] = consumerGroup;
            return context;
        }

        /// <summary>
        /// Set the <see cref="EventData"/> for an event.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The <see cref="EventContext"/> to update.</param>
        /// <param name="data">The value to set.</param>
        /// <returns>The updated context.</returns>
        internal static T SetEventData<T>(this T context, EventData data) where T : EventContext
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (data is null) throw new ArgumentNullException(nameof(data));

            context.Items[ItemsKeyEventData] = data;
            return context;
        }

        /// <summary>
        /// Set the <see cref="PartitionContext"/> for an event.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The <see cref="EventContext"/> to update.</param>
        /// <param name="partition">The value to set.</param>
        /// <returns>The updated context.</returns>
        internal static T SetPartitionContext<T>(this T context, PartitionContext partition) where T : EventContext
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (partition is null) throw new ArgumentNullException(nameof(partition));

            context.Items[ItemsKeyPartitionContext] = partition;
            return context;
        }
    }
}
