using System.Collections.Generic;

namespace System.Collections.Concurrent
{
    /// <summary>
    /// Extension methods on <see cref="ConcurrentBag{T}"/>
    /// </summary>
    internal static class ConcurrentBagExtensions
    {
        public static void AddBatch<T>(this ConcurrentBag<T> bag, IEnumerable<T> items)
        {
            if (bag is null) throw new ArgumentNullException(nameof(bag));
            if (items is null) throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                bag.Add(item);
            }
        }
    }
}
