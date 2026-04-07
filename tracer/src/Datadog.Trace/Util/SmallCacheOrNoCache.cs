// <copyright file="SmallCacheOrNoCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util;

/// <summary>
/// A wrapper around a ConcurrentDictionary that disables caching if there is too many entries.
/// To be used in situations where we expect a low cardinality, but where uncommon setups could break that assumption
/// </summary>
internal sealed class SmallCacheOrNoCache<TKey, TValue>
    where TKey : notnull
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SmallCacheOrNoCache<TKey, TValue>));

    private readonly int _maxCapacity;
    private readonly string _logFriendlyKeyType;

    private ConcurrentDictionary<TKey, TValue>? _cache = new();

    /// <param name="maxCapacity">The number of entries after which we'll consider that caching is pointless</param>
    /// <param name="logFriendlyKeyType">Only used for logging, what kind of keys are used in this cache. Use Plural.</param>
    public SmallCacheOrNoCache(int maxCapacity, string logFriendlyKeyType)
    {
        _maxCapacity = maxCapacity;
        _logFriendlyKeyType = logFriendlyKeyType;
    }

    /// <summary>
    /// Gets a value indicating whether caching is currently enabled. Caching is disabled after receiving too many entries.
    /// </summary>
    public bool IsCaching => _cache != null;

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        var cache = _cache;

        if (cache != null)
        {
            if (cache.TryGetValue(key, out var tags))
            {
                // Fast path: it's expected that most calls will end up in this branch
                return tags;
            }

            if (cache.Count <= _maxCapacity)
            {
                // Populating the cache. This path should be hit only during application warmup
                return cache.GetOrAdd(key, valueFactory);
            }

            // The assumption that this cache should be small was wrong, disabling it entirely
            // Use atomic operation to log only once
            if (Interlocked.Exchange(ref _cache, value: null) != null)
            {
                Log.Information<int, string>("More than {MaxCapacity} different {KeyType} were used, disabling cache", _maxCapacity, _logFriendlyKeyType);
            }
        }

        // Fallback: too many different keys, there might be a random part in them
        // Stop using the cache to prevent memory leaks
        return valueFactory(key);
    }

    internal void ResetForTests()
    {
        _cache = new ConcurrentDictionary<TKey, TValue>();
    }
}
