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
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static bool TryGetPropertyValue<T>(this EventData data, string key, [NotNullWhen(true)] out T? value) where T : IConvertible
    {
        if (data is null) throw new ArgumentNullException(nameof(data));

        value = default;
        if (data.SystemProperties.TryGetValue(key, out var raw_value) || data.Properties.TryGetValue(key, out raw_value))
        {
            if (raw_value.GetType() == typeof(T))
            {
                value = (T)raw_value;
                return true;
            }

            // Handle nullable differently
            var t = typeof(T);
            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                if (raw_value is null) return default;
                t = Nullable.GetUnderlyingType(t);
            }

            value = (T)Convert.ChangeType(raw_value, t!);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the property value that is associated with the specified key from
    /// <see cref="EventData.SystemProperties"/> or <see cref="EventData.Properties"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The <see cref="EventData"/> instance to use.</param>
    /// <param name="key">The key to locate.</param>
    /// <returns></returns>
    public static T? GetPropertyValue<T>(this EventData data, string key) where T : IConvertible
    {
        return data.TryGetPropertyValue<T>(key, out var value) ? value : default;
    }

    /// <summary>
    /// Gets the required property value that is associated with the specified key from
    /// <see cref="EventData.SystemProperties"/> or <see cref="EventData.Properties"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The <see cref="EventData"/> instance to use.</param>
    /// <param name="key">The key to locate.</param>
    /// <returns></returns>
    public static T GetRequiredPropertyValue<T>(this EventData data, string key) where T : IConvertible
    {
        return data.GetPropertyValue<T>(key) ?? throw new InvalidOperationException($"The property '{key}' could not be found.");
    }
}
