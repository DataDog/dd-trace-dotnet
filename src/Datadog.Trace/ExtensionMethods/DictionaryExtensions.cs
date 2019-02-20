using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            => dictionary.TryGetValue(key, out TValue value)
                   ? value
                   : default(TValue);

        public static bool TryGetValueOrDefaultAs<TAs>(this IDictionary dictionary, object key, out TAs valueAs)
        {
            valueAs = default(TAs);

            if (!dictionary.Contains(key))
            {
                return false;
            }

            try
            {
                // Try catch wrap to protect against small-chance race condition is all...
                if (dictionary[key] is TAs localObjValueAs)
                {
                    valueAs = localObjValueAs;

                    return true;
                }
            }
            catch (NotSupportedException)
            {
                // Non-generic uses NonSupportedException in most cases it seems
            }
            catch (KeyNotFoundException) { }

            return false;
        }
    }
}
