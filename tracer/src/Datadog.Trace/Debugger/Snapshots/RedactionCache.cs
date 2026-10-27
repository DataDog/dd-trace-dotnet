// <copyright file="RedactionCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Threading;

namespace Datadog.Trace.Debugger.Snapshots
{
    /// <summary>
    /// Caches redaction results. Lookups are lock-free and allocation-free.
    /// Once <see cref="Capacity"/> entries are stored, new results are no longer cached and nothing is evicted.
    /// </summary>
    internal sealed class RedactionCache<TKey>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, bool> _entries = new();
        private int _count;

        internal RedactionCache(int capacity)
        {
            Capacity = capacity;
        }

        internal int Capacity { get; }

        internal int Count => Volatile.Read(ref _count);

        internal bool TryGetValue(TKey key, out bool value) => _entries.TryGetValue(key, out value);

        internal void TryAdd(TKey key, bool value)
        {
            if (Volatile.Read(ref _count) >= Capacity)
            {
                return;
            }

            // Reserve a slot first so concurrent adds can never exceed the capacity.
            if (Interlocked.Increment(ref _count) > Capacity)
            {
                Interlocked.Decrement(ref _count);
                return;
            }

            if (!_entries.TryAdd(key, value))
            {
                Interlocked.Decrement(ref _count);
            }
        }
    }
}
