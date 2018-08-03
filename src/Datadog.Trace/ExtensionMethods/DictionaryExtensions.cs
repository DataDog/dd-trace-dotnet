using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary.TryGetValue(key, out TValue value))
            {
                return value;
            }
            else
            {
                return default(TValue);
            }
        }
    }
}
