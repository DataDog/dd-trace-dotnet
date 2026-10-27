// <copyright file="RedactionCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Datadog.Trace.Debugger.Snapshots
{
    /// <summary>
    /// Caches redaction results. Lookups are lock-free and allocation-free.
    /// Nothing is evicted: once <see cref="Capacity"/> entries are stored, <see cref="TryAdd"/> returns false
    /// and the owner replaces the full instance with a new one.
    /// </summary>
    internal sealed class RedactionCache<TKey>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, bool> _entries;
        private int _count;

        internal RedactionCache(int capacity)
        {
            Capacity = capacity;

            // Pre-sized so filling it never resizes, which would take every lock in the dictionary.
            _entries = new ConcurrentDictionary<TKey, bool>(Environment.ProcessorCount, capacity);
        }

        internal int Capacity { get; }

        internal int Count => Volatile.Read(ref _count);

        internal bool TryGetValue(TKey key, out bool value) => _entries.TryGetValue(key, out value);

        /// <returns>false if the cache is full; true if the key was added or is already present.</returns>
        internal bool TryAdd(TKey key, bool value)
        {
            if (Volatile.Read(ref _count) >= Capacity)
            {
                return false;
            }

            // Reserve a slot first so concurrent adds can never exceed the capacity.
            if (Interlocked.Increment(ref _count) > Capacity)
            {
                Interlocked.Decrement(ref _count);
                return false;
            }

            if (!_entries.TryAdd(key, value))
            {
                Interlocked.Decrement(ref _count);
            }

            return true;
        }
    }
}
