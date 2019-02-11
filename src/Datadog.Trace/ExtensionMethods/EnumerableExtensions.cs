using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class EnumerableExtensions
    {
        internal static List<T> AsList<T>(this IEnumerable<T> source)
        {
            switch (source)
            {
                case null:
                    return null;

                case List<T> sourceList:
                    return sourceList;

                default:
                    return source.ToList();
            }
        }
    }
}
