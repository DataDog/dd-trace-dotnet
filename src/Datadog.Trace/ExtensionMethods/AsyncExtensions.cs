using System;
using System.Collections.Generic;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class AsyncExtensions
    {
        internal static T GetOrSet<T>(this AsyncLocalCompat<T> source, Func<T> setter, T unsetValue = default(T))
        {
            var asyncLocalValue = source.Get();

            if (!EqualityComparer<T>.Default.Equals(asyncLocalValue, unsetValue))
            {
                return asyncLocalValue;
            }

            var newValue = setter();

            source.Set(newValue);

            return newValue;
        }
    }
}
