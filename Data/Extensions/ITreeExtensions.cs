using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data.Interfaces;

namespace WebSosync.Data.Extensions
{
    public static class ITreeExtensions
    {
        /// <summary>
        /// Extension on IList^T that returns a new list representing an hierarchical tree
        /// structure.
        /// </summary>
        /// <typeparam name="T">Item type for the list to be converted into a tree.</typeparam>
        /// <param name="input"></param>
        /// <returns>A new list, that has the item children set appropriately.</returns>
        public static IList<T> ToTree<T>(this IList<T> input) where T : class, ITree<T>
        {
            var dic = new Dictionary<int, T>(input.Count);

            foreach (var item in input)
                dic.Add(item.ID, item);

            var result = new List<T>();

            foreach (var item in input)
            {
                T parent = null;

                if (!item.ParentID.HasValue)
                {
                    result.Add(item);
                }
                else if (dic.TryGetValue(item.ParentID.Value, out parent))
                {
                    parent.Children.Add(item);
                }
            }

            return result;
        }
    }
}
