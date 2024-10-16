// <copyright file="LFUEvictionPolicy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Datadog.Trace.Debugger.Caching
{
    internal class LFUEvictionPolicy<TKey> : IEvictionPolicy<TKey>
    {
        private readonly Dictionary<TKey, FrequencyItem> _frequencyMap = new Dictionary<TKey, FrequencyItem>();
        private readonly SortedDictionary<FrequencyKey, HashSet<TKey>> _frequencySortedSet = new SortedDictionary<FrequencyKey, HashSet<TKey>>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public void Add(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_frequencyMap.ContainsKey(key))
                {
                    var now = DateTime.UtcNow;
                    _frequencyMap[key] = new FrequencyItem(1, now);
                    AddToFrequencySet(key, 1, now);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Remove(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_frequencyMap.TryGetValue(key, out var item))
                {
                    RemoveFromFrequencySet(key, item.Frequency, item.LastAccessed);
                    _frequencyMap.Remove(key);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Access(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_frequencyMap.TryGetValue(key, out var item))
                {
                    RemoveFromFrequencySet(key, item.Frequency, item.LastAccessed);
                    item.Frequency++;
                    item.LastAccessed = DateTime.UtcNow;
                    AddToFrequencySet(key, item.Frequency, item.LastAccessed);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public TKey Evict()
        {
            _lock.EnterWriteLock();
            try
            {
                if (_frequencySortedSet.Count == 0)
                {
                    throw new InvalidOperationException("Cache is empty");
                }

                var leastFrequent = _frequencySortedSet.First();
                var key = leastFrequent.Value.First();
                Remove(key);
                return key;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void AddToFrequencySet(TKey key, int frequency, DateTime lastAccessed)
        {
            var tuple = new FrequencyKey(frequency, lastAccessed);
            if (!_frequencySortedSet.TryGetValue(tuple, out var set))
            {
                set = new HashSet<TKey>();
                _frequencySortedSet[tuple] = set;
            }

            set.Add(key);
        }

        private void RemoveFromFrequencySet(TKey key, int frequency, DateTime lastAccessed)
        {
            var tuple = new FrequencyKey(frequency, lastAccessed);
            if (_frequencySortedSet.TryGetValue(tuple, out var set))
            {
                set.Remove(key);
                if (set.Count == 0)
                {
                    _frequencySortedSet.Remove(tuple);
                }
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        private record struct FrequencyKey(int Frequency, DateTime LastAccessed);

        private class FrequencyItem
        {
            public FrequencyItem(int frequency, DateTime lastAccessed)
            {
                Frequency = frequency;
                LastAccessed = lastAccessed;
            }

            public int Frequency { get; set; }

            public DateTime LastAccessed { get; set; }
        }
    }
}
