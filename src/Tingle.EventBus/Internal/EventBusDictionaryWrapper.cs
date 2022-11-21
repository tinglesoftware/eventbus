namespace Tingle.EventBus.Internal;

/// <summary>A wrapper around <see cref="IDictionary{TKey, TValue}"/> for EventBus related functionality.</summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
/// <remarks>
/// This type exists instead of extension methods so that it is exposed to other projects/packages in the Tingle.EventBus group
/// while discouraging external projects/packages/application from using the methods
/// </remarks>
public sealed class EventBusDictionaryWrapper<TKey, TValue> where TKey : notnull
{
    private readonly IDictionary<TKey, TValue> dictionary;

    /// <summary>Creates an instance of <see cref="EventBusDictionaryWrapper{TKey, TValue}"/>.</summary>
    public EventBusDictionaryWrapper() : this(new Dictionary<TKey, TValue>()) { }

    /// <summary>Creates an instance of <see cref="EventBusDictionaryWrapper{TKey, TValue}"/>.</summary>
    /// <param name="dictionary">The <see cref="IDictionary{TKey, TValue}"/> to use.</param>
    /// <exception cref="ArgumentException">dictionary is read-only.</exception>
    /// <exception cref="ArgumentNullException">dictionary is null.</exception>
    public EventBusDictionaryWrapper(IDictionary<TKey, TValue> dictionary)
    {
        if (dictionary.IsReadOnly)
        {
            throw new ArgumentException("Read-only dictionaries are not allowed.", nameof(dictionary));
        }

        this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
    }

    /// <summary>The dictionary managed.</summary>
    public IDictionary<TKey, TValue> Dictionary => dictionary;

    /// <summary>
    /// Adds an element with the provided key and value,
    /// provided the value is not equal to the type's default value (or empty for strings).
    /// </summary>
    /// <param name="key">The object to use as the key of the element to add.</param>
    /// <param name="value">The object to use as the value of the element to add.</param>
    /// <exception cref="ArgumentNullException">key is null.</exception>
    /// <exception cref="ArgumentException">An element with the same key already exists in the dictionary.</exception>
    /// <exception cref="NotSupportedException">The dictionary is read-only.</exception>
    /// <returns></returns>
    public EventBusDictionaryWrapper<TKey, TValue> AddIfNotDefault(TKey key, TValue? value)
    {
        if (value is not null || value is string s && !string.IsNullOrWhiteSpace(s))
        {
            dictionary[key] = value;
        }

        return this;
    }
}
