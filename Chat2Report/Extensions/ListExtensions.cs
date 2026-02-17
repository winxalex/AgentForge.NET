using System;
using System.Collections.Generic;

namespace Chat2Report.Extensions
{
    public static class ListExtensions
    {
        public static int RemoveAll<T>(this IList<T> list, Predicate<T> match)
        {
            if (list is null)
                throw new ArgumentNullException(nameof(list));
            if (match is null)
                throw new ArgumentNullException(nameof(match));

            int removedCount = 0;
            // Iterate backwards to correctly remove items without affecting the index order.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (match(list[i]))
                {
                    list.RemoveAt(i);
                    removedCount++;
                }
            }
            return removedCount;
        }
    }
}
