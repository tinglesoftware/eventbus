using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SNSAttribute = Amazon.SimpleNotificationService.Model.MessageAttributeValue;
using SQSAttribute = Amazon.SQS.Model.MessageAttributeValue;

namespace Tingle.EventBus.Transports.Amazon.Sqs;

/// <summary>
/// Extension methods on <see cref="PublishRequest"/>,
/// <see cref="SendMessageRequest"/>, <see cref="SendMessageBatchRequestEntry"/>,
/// and <see cref="Message"/>
/// </summary>
internal static class MessageAttributeExtensions
{
    private static Dictionary<string, TAttribute> SetAttribute<TAttribute>(this Dictionary<string, TAttribute> attributes, Func<string, TAttribute> creator, string key, string? value)
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));
        if (creator is null) throw new ArgumentNullException(nameof(creator));
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
        }

        // set the value when not null or empty
        if (!string.IsNullOrWhiteSpace(value))
        {
            attributes[key] = creator(value);
        }

        return attributes;
    }

    public static Dictionary<string, TAttribute> SetAttributes<TAttribute>(this Dictionary<string, TAttribute> attributes, Func<string, TAttribute> creator, EventContext @event)
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));
        if (@event is null) throw new ArgumentNullException(nameof(@event));
        if (creator is null) throw new ArgumentNullException(nameof(creator));

        attributes.SetAttribute(creator, MetadataNames.ContentType, @event.ContentType?.ToString())
                  .SetAttribute(creator, MetadataNames.CorrelationId, @event.CorrelationId)
                  .SetAttribute(creator, MetadataNames.RequestId, @event.RequestId)
                  .SetAttribute(creator, MetadataNames.InitiatorId, @event.InitiatorId)
                  .SetAttribute(creator, MetadataNames.ActivityId, Activity.Current?.Id);

        return attributes;
    }

    public static PublishRequest SetAttributes(this PublishRequest request, EventContext @event)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        static SNSAttribute creator(string v) => new() { DataType = "String", StringValue = v };

        request.MessageAttributes.SetAttributes(creator, @event);
        return request;
    }

    public static SendMessageRequest SetAttributes(this SendMessageRequest request, EventContext @event)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        static SQSAttribute creator(string v) => new() { DataType = "String", StringValue = v };

        request.MessageAttributes.SetAttributes(creator, @event);
        return request;
    }

    public static SendMessageBatchRequestEntry SetAttributes(this SendMessageBatchRequestEntry request, EventContext @event)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        static SQSAttribute creator(string v) => new() { DataType = "String", StringValue = v };

        request.MessageAttributes.SetAttributes(creator, @event);
        return request;
    }

    public static bool TryGetAttribute(this Message message, string key, [NotNullWhen(true)] out string? value)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
        }

        if (message.Attributes.TryGetValue(key, out value)) return true;

        if (message.MessageAttributes.TryGetValue(key, out var attr))
        {
            value = attr.StringValue;
            return true;
        }

        value = default;
        return false;
    }
}
