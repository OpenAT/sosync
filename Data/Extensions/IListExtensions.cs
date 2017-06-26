using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Extensions
{
    public static class IListExtensions
    {
        /// <summary>
        /// <see cref="IList"/>-Extension to build a tree structrues from ID and ParentID fields.
        /// </summary>
        /// <typeparam name="T">The list object type.</typeparam>
        /// <param name="input">The input list to build the tree from.</param>
        /// <param name="id">Lambda expression to return the ID field.</param>
        /// <param name="parentId">Lambda expression to return the parent ID field.</param>
        /// <param name="children">Lambda expression to return the children <see cref="IList"/>.</param>
        /// <returns>A new list, only containing the top level nodes.</returns>
        public static IList<T> ToTree<T>(this IList<T> input, Func<T, int> id, Func<T, int?> parentId, Func<T, IList<T>> children) where T : class
        {
            var dic = new Dictionary<int, T>(input.Count);

            foreach (var item in input)
                dic.Add(id(item), item);

            var result = new List<T>();

            foreach (var item in input)
            {
                T parent = null;

                if (!parentId(item).HasValue)
                {
                    result.Add(item);
                }
                else if (dic.TryGetValue(parentId(item).Value, out parent))
                {
                    children(parent).Add(item);
                }
            }

            return result;
        }
    }
}
