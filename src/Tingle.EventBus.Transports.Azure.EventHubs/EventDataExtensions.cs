using System.Diagnostics.CodeAnalysis;

namespace Azure.Messaging.EventHubs;

/// <summary>
/// Extension methods on <see cref="EventData"/>.
/// </summary>
public static class EventDataExtensions
{
    internal static bool TryGetPropertyValue(this EventData data, string key, [NotNullWhen(true)] out object? value)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace.", nameof(key));
        }

        return data.SystemProperties.TryGetValue(key, out value)
            || data.Properties.TryGetValue(key, out value);
    }

    internal static T? GetPropertyValue<T>(this EventData data, string key)
    {
        return data.TryGetPropertyValue(key, out var value) && value is not null ? (T?)value : default;
    }
}
