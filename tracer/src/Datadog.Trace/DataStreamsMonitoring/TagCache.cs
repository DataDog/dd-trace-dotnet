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
/// Bounded tag cache that replaces the least recently used idle entry under capacity pressure.
/// Active entries stay cached even when the working set exceeds the capacity.
/// </summary>
internal sealed class TagCache<TKey, TValue>
    where TKey : IEquatable<TKey>
{
    internal const int IdleTimeoutSeconds = 60;

    private readonly ConcurrentDictionary<TKey, Entry> _cache = new();
    private readonly object _evictionLock = new();
    private readonly int _capacity;
    private readonly Func<long> _getTimestampSeconds;
    private int _count;
    private long _nextEvictionCheck;

    // A monotonic clock with second precision avoids shared writes on every cache hit.
    internal TagCache(int capacity, Func<long>? getTimestampSeconds = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _getTimestampSeconds = getTimestampSeconds ?? (static () => Stopwatch.GetTimestamp() / Stopwatch.Frequency);
    }

    internal TValue GetOrCreate<TState>(TKey key, TState state, Func<TKey, TState, TValue> factory)
    {
        var now = _getTimestampSeconds();
        if (_cache.TryGetValue(key, out var existing))
        {
            existing.Touch(now);
            return existing.Value;
        }

        var value = factory(key, state);
        // A full, active cache bypasses both the eviction lock and the scan on misses.
        if (Volatile.Read(ref _count) >= _capacity && now < Volatile.Read(ref _nextEvictionCheck))
        {
            return value;
        }

        lock (_evictionLock)
        {
            now = _getTimestampSeconds();
            if (_cache.TryGetValue(key, out existing))
            {
                existing.Touch(now);
                return existing.Value;
            }

            if (_count >= _capacity && (now < _nextEvictionCheck || !TryEvictIdleEntry(now)))
            {
                return value;
            }

            _cache[key] = new Entry(value, now);
            Volatile.Write(ref _count, _count + 1);
            return value;
        }
    }

    // Called only under the insertion lock. Hits can continue while the entries are scanned.
    private bool TryEvictIdleEntry(long now)
    {
        TKey oldestKey = default!;
        Entry? oldestEntry = null;
        var oldestAccess = long.MaxValue;
        foreach (var item in _cache)
        {
            var lastAccess = item.Value.LastAccess;
            if (lastAccess < oldestAccess)
            {
                oldestAccess = lastAccess;
                oldestKey = item.Key;
                oldestEntry = item.Value;
            }
        }

        if (oldestEntry is not null && now - oldestEntry.LastAccess >= IdleTimeoutSeconds)
        {
            _cache.TryRemove(oldestKey, out _);
            Volatile.Write(ref _count, _count - 1);
            return true;
        }

        // Recheck when the oldest observed entry could become idle. If it was refreshed
        // during the scan, wait at least a second rather than rescanning on every miss.
        Volatile.Write(ref _nextEvictionCheck, Math.Max(now + 1, oldestAccess + IdleTimeoutSeconds));
        return false;
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
