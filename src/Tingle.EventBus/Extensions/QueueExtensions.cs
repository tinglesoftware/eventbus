using System.Collections.Generic;

namespace System.Collections.Concurrent
{
    /// <summary>
    /// Extension methods on <see cref="Queue{T}"/> and <see cref="ConcurrentQueue{T}"/>
    /// </summary>
    internal static class QueueExtensions
    {
        public static void EnqueueBatch<T>(this Queue<T> queue, IEnumerable<T> items)
        {
            if (queue is null) throw new ArgumentNullException(nameof(queue));
            if (items is null) throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                queue.Enqueue(item);
            }
        }

        public static void EnqueueBatch<T>(this ConcurrentQueue<T> queue, IEnumerable<T> items)
        {
            if (queue is null) throw new ArgumentNullException(nameof(queue));
            if (items is null) throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                queue.Enqueue(item);
            }
        }

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
