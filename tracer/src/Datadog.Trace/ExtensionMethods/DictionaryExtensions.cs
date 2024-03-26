// <copyright file="DictionaryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class DictionaryExtensions
    {
        // Dictionary<TKey, TValue> implements both IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue>,
        // this overload resolved the ambiguity.
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        {
            return GetValueOrDefault((IDictionary<TKey, TValue>)dictionary, key);
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(dictionary));
            }

            return dictionary.TryGetValue(key, out var value)
                       ? value
                       : default;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(dictionary));
            }

            return dictionary.TryGetValue(key, out var value)
                       ? value
                       : default;
        }

        public static TValue GetValueOrDefault<TValue>(this IDictionary dictionary, object key)
        {
            if (dictionary == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(dictionary));
            }

            return dictionary.TryGetValue(key, out TValue value)
                       ? value
                       : default;
        }

        public static bool TryGetValue<TValue>(this IDictionary dictionary, object key, out TValue value)
        {
            if (dictionary == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(dictionary));
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

        public static bool IsEmpty<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            if (dictionary is ConcurrentDictionary<TKey, TValue> concurrentDictionary)
            {
                return concurrentDictionary.IsEmpty;
            }

            return dictionary.Count == 0;
        }

        public static bool IsNullOrEmpty<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary) => dictionary?.IsEmpty() ?? true;

        public static TV GetAndRemove<TK, TV>(this Dictionary<TK, TV> map, TK key)
            where TK : notnull
            where TV : class
        {
            if (map != null && map.TryGetValue(key, out var val))
            {
                map.Remove(key);
                return val;
            }

            return null;
        }

        public static TV Get<TK, TV>(this Dictionary<TK, TV> map, TK key, Func<TK, TV> computeIfAbsent)
            where TK : notnull
            where TV : class
        {
            if (map.TryGetValue(key, out var val))
            {
                return val;
            }

            var newVal = computeIfAbsent(key);
            map[key] = newVal;
            return newVal;
        }
    }
}
