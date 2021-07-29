namespace System.Collections.Generic
{
    /// <summary>
    /// Extension methods on <see cref="IDictionary{TKey, TValue}"/>
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Adds an element with the provided key and value to the <see cref="IDictionary{TKey, TValue}"/>,
        /// provided the value is not equal to the type's default value (or empty for strings).
        /// </summary>
        /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
        /// <param name="dictionary">The dictionary to use</param>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        /// <exception cref="ArgumentNullException">key is null.</exception>
        /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="IDictionary{TKey, TValue}"/>.</exception>
        /// <exception cref="NotSupportedException">The <see cref="IDictionary{TKey, TValue}"/> is read-only.</exception>
        /// <returns></returns>
        public static IDictionary<TKey, TValue?> AddIfNotDefault<TKey, TValue>(this IDictionary<TKey, TValue?> dictionary, TKey key, TValue? value)
        {
            if (!EqualityComparer<TValue?>.Default.Equals(value, default)
                || (value is string s && !string.IsNullOrWhiteSpace(s)))
            {
                dictionary[key] = value;
            }

            return dictionary;
        }
    }
}
