// <copyright file="ConcurrentDictionaryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP
#nullable enable

using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.ExtensionMethods;

/// <summary>
/// Polyfills for the <see cref="ConcurrentDictionary{TKey,TValue}"/> overloads that take a factory argument, which are
/// only available on .NET Core. They follow the same algorithm as the .NET Core implementations, so the factories can
/// still run more than once when callers race, but they don't allocate a closure.
/// </summary>
internal static class ConcurrentDictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue, TArg>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument)
        where TKey : notnull
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

#pragma warning disable MA0242 // Use a value factory - this is the factory overload: like the .NET Core implementation, the factory only runs after TryGetValue misses
        return dictionary.GetOrAdd(key, valueFactory(key, factoryArgument));
#pragma warning restore MA0242
    }

    public static TValue AddOrUpdate<TKey, TValue, TArg>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TArg, TValue> addValueFactory, Func<TKey, TValue, TArg, TValue> updateValueFactory, TArg factoryArgument)
        where TKey : notnull
    {
        while (true)
        {
            if (dictionary.TryGetValue(key, out var oldValue))
            {
                var newValue = updateValueFactory(key, oldValue, factoryArgument);
                if (dictionary.TryUpdate(key, newValue, oldValue))
                {
                    return newValue;
                }
            }
            else
            {
                var newValue = addValueFactory(key, factoryArgument);
                if (dictionary.TryAdd(key, newValue))
                {
                    return newValue;
                }
            }
        }
    }
}
#endif
