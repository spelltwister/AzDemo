using System.Collections.Generic;

namespace Utility
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Chunks the collection into size chunks
        /// </summary>
        /// <typeparam name="T">Type of item in the collection</typeparam>
        /// <param name="collection">
        /// Collection to chunk
        /// </param>
        /// <param name="size">
        /// Size of the chunks
        /// </param>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> containing the chunks
        /// </returns>
        public static IEnumerable<List<T>> Chunk<T>(this IEnumerable<T> collection, int size = 100)
        {
            int innerCounter = 0;
            List<T> ret = new List<T>(size);
            foreach (T item in collection)
            {
                ret.Add(item);
                if (++innerCounter == size)
                {
                    yield return ret;
                    ret = new List<T>(size);
                    innerCounter = 0;
                }
            }

            if (ret.Count > 0)
            {
                yield return ret;
            }

            yield break;
        }
    }
}