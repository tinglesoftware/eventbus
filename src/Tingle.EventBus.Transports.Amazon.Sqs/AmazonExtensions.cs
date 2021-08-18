﻿using System;
using System.Diagnostics.CodeAnalysis;

namespace Amazon.SimpleNotificationService.Model
{
    /// <summary>
    /// Extension methods on <see cref="PublishRequest"/>
    /// </summary>
    internal static class PublishRequestExtensions
    {
        public static PublishRequest SetAttribute(this PublishRequest request, string key, string? value)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace", nameof(key));
            }

            // set the value when not null or empty
            if (!string.IsNullOrWhiteSpace(value))
            {
                request.MessageAttributes[key] = new MessageAttributeValue { DataType = "String", StringValue = value };
                return request;
            }

            return request;
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
}
