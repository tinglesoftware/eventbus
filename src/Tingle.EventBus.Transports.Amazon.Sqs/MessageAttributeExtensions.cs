using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
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
    private static void SetAttribute<TAttribute>(this Dictionary<string, TAttribute> attributes, Func<string, TAttribute> creator, string key, string? value)
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
    }

    public static PublishRequest SetAttribute(this PublishRequest request, string key, string? value)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        request.MessageAttributes.SetAttribute(
            creator: v => new SNSAttribute { DataType = "String", StringValue = v },
            key: key,
            value: value);

        return request;
    }

    public static SendMessageRequest SetAttribute(this SendMessageRequest request, string key, string? value)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        request.MessageAttributes.SetAttribute(
            creator: v => new SQSAttribute { DataType = "String", StringValue = v },
            key: key,
            value: value);

        return request;
    }

    public static SendMessageBatchRequestEntry SetAttribute(this SendMessageBatchRequestEntry request, string key, string? value)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        request.MessageAttributes.SetAttribute(
            creator: v => new SQSAttribute { DataType = "String", StringValue = v },
            key: key,
            value: value);

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
