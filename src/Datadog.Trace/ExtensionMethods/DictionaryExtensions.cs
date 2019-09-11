using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            return dictionary.TryGetValue(key, out var value)
                       ? value
                       : default;
        }

        public static TValue GetValueOrDefault<TValue>(this IDictionary dictionary, object key)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            return dictionary.TryGetValue(key, out TValue value)
                       ? value
                       : default;
        }

        public static bool TryGetValue<TValue>(this IDictionary dictionary, object key, out TValue value)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            object valueObj;

            try
            {
                // per its contract, IDictionary.Item[] should return null instead of throwing an exception
                // if a key is not found, but let's use try/catch to be defensive against misbehaving implementations
                valueObj = dictionary[key];
            }
            catch
            {
                valueObj = null;
            }

            switch (valueObj)
            {
                case TValue valueTyped:
                    value = valueTyped;
                    return true;
                case IConvertible convertible:
                    value = (TValue)convertible.ToType(typeof(TValue), provider: null);
                    return true;
                default:
                    value = default;
                    return false;
            }
        }
    }
}
