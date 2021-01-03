using System;

namespace Amazon.SimpleNotificationService.Model
{
    /// <summary>
    /// Extension methods on <see cref="PublishRequest"/>
    /// </summary>
    internal static class PublishRequestExtensions
    {
        public static void SetAttribute(this PublishRequest request, string key, string value)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(value)) return;
            request.MessageAttributes[key] = new MessageAttributeValue { DataType = "String", StringValue = value };
        }
    }
}

namespace Amazon.SQS.Model
{
    /// <summary>
    /// Extension methods on <see cref="Message"/>
    /// </summary>
    internal static class MessageExtensions
    {
        public static bool TryGetAttribute(this Message message, string key, out string value)
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
}
