using Azure.Messaging.EventHubs;
using System.Diagnostics.CodeAnalysis;

namespace Tingle.EventBus.Transports.Azure.EventHubs;

/// <summary>
/// Extension methods on <see cref="EventData"/>.
/// </summary>
public static class EventDataExtensions
{
    /// <summary>
    /// Gets the property value that is associated with the specified key from
    /// <see cref="EventData.SystemProperties"/> or <see cref="EventData.Properties"/>.
    /// </summary>
    /// <param name="data">The <see cref="EventData"/> instance to use.</param>
    /// <param name="key">The key to locate.</param>
    /// <param name="value">
    /// When this method returns, the value associated with the specified key, if the key is found;
    /// otherwise, the default value for the type of the value parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> true if the <see cref="EventData"/> instance contains a property that
    /// has the specified key in <see cref="EventData.SystemProperties"/> or <see cref="EventData.Properties"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty or whitespace.</exception>
    public static bool TryGetPropertyValue(this EventData data, string key, [NotNullWhen(true)] out object? value)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or whitespace.", nameof(key));
        }

        return data.SystemProperties.TryGetValue(key, out value)
            || data.Properties.TryGetValue(key, out value);
    }

    /// <summary>
    /// Gets the property value that is associated with the specified key from
    /// <see cref="EventData.SystemProperties"/> or <see cref="EventData.Properties"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The <see cref="EventData"/> instance to use.</param>
    /// <param name="key">The key to locate.</param>
    /// <returns></returns>
    public static T? GetPropertyValue<T>(this EventData data, string key)
    {
        return data.TryGetPropertyValue(key, out var value) && value is not null ? (T?)value : default;
    }
}
