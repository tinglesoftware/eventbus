using Tingle.EventBus.Internal;

namespace System.Collections.Generic;

/// <summary>Extension methods on <see cref="IDictionary{TKey, TValue}"/>.</summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Creates an <see cref="EventBusDictionaryWrapper{TKey, TValue}"/>
    /// wrapping around the provided <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to use</param>
    /// <returns></returns>
    public static EventBusDictionaryWrapper<TKey, TValue> ToEventBusWrapper<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        where TKey : notnull => new(dictionary);
}
