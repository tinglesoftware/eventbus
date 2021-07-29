using Azure.Messaging.ServiceBus;
using System;

namespace Tingle.EventBus
{
    /// <summary>
    /// Extension methods on <see cref="EventContext"/> and <see cref="EventContext{T}"/>.
    /// </summary>
    public static class EventContextExtensions
    {
        internal const string ItemsKeyMessage =  "azure.servicebus.message";

        /// <summary>
        /// Gets the <see cref="ServiceBusReceivedMessage"/> associated with the specified <see cref="EventContext"/>
        /// if the event uses Azure Service Bus transport.
        /// </summary>
        /// <param name="context">The <see cref="EventContext"/> to use.</param>
        /// <param name="message">
        /// When this method returns, the value, if found; otherwise,
        /// the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// true if the message is found; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">The context is null</exception>
        public static bool TryGetServiceBusMessage(this EventContext context, out ServiceBusReceivedMessage? message)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            if (context.Items.TryGetValue(ItemsKeyMessage, out var obj) && obj is ServiceBusReceivedMessage msg)
            {
                message = msg;
                return true;
            }

            message = default;
            return false;
        }

        /// <summary>
        /// Set the <see cref="ServiceBusReceivedMessage"/> for an event.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The <see cref="EventContext"/> to update.</param>
        /// <param name="message">The value to set.</param>
        /// <returns>The updated context.</returns>
        internal static T SetServiceBusMessage<T>(this T context, ServiceBusReceivedMessage message) where T : EventContext
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (message is null) throw new ArgumentNullException(nameof(message));

            context.Items[ItemsKeyMessage] = message;
            return context;
        }
    }
}
