using System.Diagnostics.CodeAnalysis;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus;

internal static class ExecutionHelper
{
    /// <summary>Deserialize an event from a stream of bytes.</summary>
    /// <typeparam name="T">The event type to be deserialized.</typeparam>
    /// <param name="serializer">The <see cref="IEventSerializer"/> to use.</param>
    /// <param name="context">The <see cref="DeserializationContext"/> to use.</param>
    /// <param name="publisher">The <see cref="IEventPublisher"/> to use.</param>
    /// <param name="cancellationToken"></param>
    public static async Task<EventContext> DeserializeToContextAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] T>(IEventSerializer serializer,
                                                                                                                             DeserializationContext context,
                                                                                                                             IEventPublisher publisher,
                                                                                                                             CancellationToken cancellationToken = default) where T : class
    {
        var envelope = await serializer.DeserializeAsync<T>(context, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deserialization from '{context.Registration.EventType.Name}' resulted in null which is not allowed."); // throwing helps track the error

        // Create the context
        return context.Deadletter
            ? new DeadLetteredEventContext<T>(publisher: publisher, envelope: envelope, deserializationContext: context)
            : new EventContext<T>(publisher: publisher, envelope: envelope, deserializationContext: context);
    }

    public static async Task ConsumeAsync<[DynamicallyAccessedMembers(TrimmingHelper.Event)] TEvent>(IEventConsumer consumer,
                                                                                                     EventRegistration registration,
                                                                                                     EventConsumerRegistration ecr,
                                                                                                     EventContext @event,
                                                                                                     CancellationToken cancellationToken) where TEvent : class
    {
        // Consume the event with the consumer appropriately
        if (consumer is IEventConsumer<TEvent> consumer_normal && @event is EventContext<TEvent> evt_normal)
        {
            // Invoke handler method, with resilience pipeline
            await registration.ExecutionPipeline.ExecuteAsync(
                async ct => await consumer_normal.ConsumeAsync(evt_normal, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }
        else if (consumer is IDeadLetteredEventConsumer<TEvent> consumer_deadletter && @event is DeadLetteredEventContext<TEvent> evt_deadletter)
        {
            // Invoke handler method, with resilience pipelines
            await registration.ExecutionPipeline.ExecuteAsync(
                async ct => await consumer_deadletter.ConsumeAsync(evt_deadletter, ct).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Consumer '{ecr.ConsumerType.FullName}' can't consume '{@event.GetType().FullName}' events. This shouldn't happen. Please file an issue.");
        }
    }

    [RequiresDynamicCode(MessageStrings.GenericsDynamicCodeMessage)]
    [RequiresUnreferencedCode(MessageStrings.GenericsUnreferencedCodeMessage)]
    public static DeserializerDelegate MakeDelegate([DynamicallyAccessedMembers(TrimmingHelper.Event)] Type eventType)
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var mi = typeof(ExecutionHelper).GetMethod(nameof(DeserializeToContextAsync), flags) ?? throw new InvalidOperationException("Methods should not be null");
        var method = mi.MakeGenericMethod([eventType]);
        return (serializer, context, publisher, cancellationToken) => (Task<EventContext>)method.Invoke(null, [serializer, context, publisher, cancellationToken])!;
    }

    [RequiresDynamicCode(MessageStrings.GenericsDynamicCodeMessage)]
    [RequiresUnreferencedCode(MessageStrings.GenericsUnreferencedCodeMessage)]
    public static ConsumeDelegate MakeDelegate([DynamicallyAccessedMembers(TrimmingHelper.Event)] Type eventType, [DynamicallyAccessedMembers(TrimmingHelper.Consumer)] Type consumerType)
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var mi = typeof(ExecutionHelper).GetMethod(nameof(ConsumeAsync), flags) ?? throw new InvalidOperationException("Methods should not be null");
        var method = mi.MakeGenericMethod([eventType]);
        return (consumer, registration, ecr, context, cancellationToken) => (Task)method.Invoke(null, [consumer, registration, ecr, context, cancellationToken])!;
    }
}
