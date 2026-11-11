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
    /// Nothing is evicted: once <see cref="Capacity"/> entries are stored, <see cref="TryAdd"/> rejects new keys
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

            // Pre-sized to avoid repeated growth.
            _entries = new ConcurrentDictionary<TKey, bool>(Environment.ProcessorCount, capacity);
        }

        internal int Capacity { get; }

        internal int Count => Volatile.Read(ref _count);

        internal bool TryGetValue(TKey key, out bool value) => _entries.TryGetValue(key, out value);

        /// <returns>true if the key was added or is already present; false if the cache is full and doesn't hold the key.</returns>
        internal bool TryAdd(TKey key, bool value)
        {
            if (!TryReserveSlot())
            {
                return _entries.ContainsKey(key);
            }

            if (!_entries.TryAdd(key, value))
            {
                Interlocked.Decrement(ref _count);
            }

            return true;
        }

        // Reserving the slot before the add means concurrent adds can never exceed the capacity.
        private bool TryReserveSlot()
        {
            // The plain read skips the contended increment once the cache is full.
            if (Volatile.Read(ref _count) >= Capacity)
            {
                return false;
            }

            if (Interlocked.Increment(ref _count) <= Capacity)
            {
                return true;
            }

            Interlocked.Decrement(ref _count);
            return false;
        }
    }
}
