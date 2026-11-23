// <copyright file="RedactionCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Debugger.Snapshots
{
    /// <summary>
    /// Caches redaction results. Lookups are lock-free and allocation-free.
    /// Nothing is evicted: once <see cref="Capacity"/> entries are stored, <see cref="TryAdd"/> rejects new keys.
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

        [TestingAndPrivateOnly]
        internal int Capacity { get; }

        [TestingOnly]
        internal int Count => Volatile.Read(ref _count);

        internal bool TryGetValue(TKey key, out bool value) => _entries.TryGetValue(key, out value);

        /// <returns>true if the key was added or is already present; false if the cache is full and doesn't hold the key.</returns>
        [TestingAndPrivateOnly]
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

        /// <summary>
        /// Adds the entry. When this cache is full, installs a new cache in <paramref name="field"/> that holds only this entry;
        /// if <paramref name="field"/> already holds a different cache (one that another thread or a configuration change
        /// installed), that cache is kept and the entry is not cached.
        /// </summary>
        internal void AddOrReplace(ref RedactionCache<TKey> field, TKey key, bool value)
        {
            if (!TryAdd(key, value))
            {
                TryReplace(ref field, key, value);
            }
        }

        /// <returns>false if <paramref name="field"/> no longer holds this cache; the cache it holds is kept.</returns>
        [TestingAndPrivateOnly]
        internal bool TryReplace(ref RedactionCache<TKey> field, TKey key, bool value)
        {
            var fresh = new RedactionCache<TKey>(Capacity);
            fresh.TryAdd(key, value);
            return ReferenceEquals(Interlocked.CompareExchange(ref field, fresh, this), this);
        }

        // Reserving the slot before the add means concurrent adds can never exceed the capacity.
        private bool TryReserveSlot()
        {
            // Checking first skips the contended increment once the cache is full.
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
