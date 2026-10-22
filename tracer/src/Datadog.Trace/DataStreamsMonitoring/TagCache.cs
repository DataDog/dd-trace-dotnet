// <copyright file="TagCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Bounded tag cache that replaces entries idle for a minute under capacity pressure.
/// Active entries stay cached even when the working set exceeds the capacity.
/// </summary>
internal sealed class TagCache<TKey, TValue>
    where TKey : IEquatable<TKey>
{
    internal const int IdleTimeoutSeconds = 60;

    // Production uses one shared cache per key/value type; tests can construct isolated instances.
    private static TagCache<TKey, TValue>? _sharedCache;

    private readonly ConcurrentDictionary<TKey, Entry> _cache = new();
    private readonly object _cacheModificationLock = new();
    private readonly int _capacity;
    private readonly Func<long>? _getTimestampSeconds;
    private int _count;
    private long _nextEvictionCheck;

    // A monotonic clock with second precision avoids shared writes on every cache hit.
    // The optional clock lets tests advance time without waiting a real minute; production uses Stopwatch.
    internal TagCache(int capacity, Func<long>? getTimestampSeconds = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _getTimestampSeconds = getTimestampSeconds;
    }

    internal static TValue GetOrCreate(TKey key, Func<TKey, TValue> factory, int capacity)
        => GetSharedCache(capacity).GetOrCreate(key, factory);

    internal TValue GetOrCreate(TKey key, Func<TKey, TValue> factory)
    {
        var now = GetTimestampSeconds();
        if (_cache.TryGetValue(key, out var existing))
        {
            existing.Touch(now);
            return existing.Value;
        }

        var value = factory(key);
        // A full, active cache bypasses both the cache modification lock and the scan on misses.
        if (Volatile.Read(ref _count) >= _capacity && now < Volatile.Read(ref _nextEvictionCheck))
        {
            return value;
        }

        lock (_cacheModificationLock)
        {
            now = GetTimestampSeconds();
            if (_cache.TryGetValue(key, out existing))
            {
                existing.Touch(now);
                return existing.Value;
            }

            if (_count >= _capacity && (now < _nextEvictionCheck || !TryEvictIdleEntries(now)))
            {
                return value;
            }

            _cache[key] = new Entry(value, now);
            Volatile.Write(ref _count, _count + 1);
            return value;
        }
    }

    // The first manager to use this key/value type determines its process-wide capacity.
    // Later managers reuse the cache even if their configured capacity differs.
    private static TagCache<TKey, TValue> GetSharedCache(int capacity)
    {
        var cache = Volatile.Read(ref _sharedCache);
        if (cache is not null)
        {
            return cache;
        }

        var created = new TagCache<TKey, TValue>(capacity);
        return Interlocked.CompareExchange(ref _sharedCache, created, null) ?? created;
    }

    // The production clock is read directly rather than through a delegate, since it runs on every hit.
    private long GetTimestampSeconds()
        => _getTimestampSeconds is null ? Stopwatch.GetTimestamp() / Stopwatch.Frequency : _getTimestampSeconds();

    // Called only under the cache modification lock. Hits can continue while the entries are scanned.
    // Every idle entry is removed in a single pass, so a burst of misses for new keys pays for
    // one scan rather than one scan per miss.
    private bool TryEvictIdleEntries(long now)
    {
        var evicted = 0;
        var oldestActiveAccess = long.MaxValue;
        foreach (var item in _cache)
        {
            var lastAccess = item.Value.LastAccess;
            if (now - lastAccess < IdleTimeoutSeconds)
            {
                oldestActiveAccess = Math.Min(oldestActiveAccess, lastAccess);
                continue;
            }

            if (!_cache.TryRemove(item.Key, out var removed))
            {
                continue;
            }

            // A concurrent hit may have refreshed the entry after the idle check. Inserts only
            // happen under this lock, so the entry can be restored without overwriting anything.
            var refreshedAccess = removed.LastAccess;
            if (now - refreshedAccess < IdleTimeoutSeconds)
            {
                _cache[item.Key] = removed;
                oldestActiveAccess = Math.Min(oldestActiveAccess, refreshedAccess);
                continue;
            }

            evicted++;
        }

        Volatile.Write(ref _count, _count - evicted);

        // Every remaining entry is active, so none can become idle before the oldest of them does.
        // Entries inserted later are newer, so they cannot become idle earlier either.
        Volatile.Write(ref _nextEvictionCheck, oldestActiveAccess == long.MaxValue ? now : Math.Max(now + 1, oldestActiveAccess + IdleTimeoutSeconds));
        return evicted > 0;
    }

    private sealed class Entry
    {
        private long _lastAccess;

        internal Entry(TValue value, long now)
        {
            Value = value;
            _lastAccess = now;
        }

        internal TValue Value { get; }

        internal long LastAccess => Volatile.Read(ref _lastAccess);

        internal void Touch(long now)
        {
            var previous = LastAccess;
            while (now > previous)
            {
                var observed = Interlocked.CompareExchange(ref _lastAccess, now, previous);
                if (observed == previous)
                {
                    return;
                }

                previous = observed;
            }
        }
    }
}
