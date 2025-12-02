// <copyright file="LfuEvictionPolicy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Datadog.Trace.Debugger.Caching
{
    internal sealed class LfuEvictionPolicy<TKey> : IEvictionPolicy<TKey>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, FrequencyItem> _frequencyMap = new Dictionary<TKey, FrequencyItem>();
        private readonly SortedDictionary<FrequencyKey, HashSet<TKey>> _frequencySortedSet = new SortedDictionary<FrequencyKey, HashSet<TKey>>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private long _accessCounter;

        public void Add(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_frequencyMap.ContainsKey(key))
                {
                    var accessOrder = Interlocked.Increment(ref _accessCounter);
                    _frequencyMap[key] = new FrequencyItem(1, accessOrder);
                    AddToFrequencySet(key, 1, accessOrder);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void Remove(TKey key)
        {
            if (_frequencyMap.TryGetValue(key, out var item))
            {
                RemoveFromFrequencySet(key, item.Frequency, item.AccessOrder);
                _frequencyMap.Remove(key);
            }
        }

        public void Access(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_frequencyMap.TryGetValue(key, out var item))
                {
                    RemoveFromFrequencySet(key, item.Frequency, item.AccessOrder);
                    item.Frequency++;
                    item.AccessOrder = Interlocked.Increment(ref _accessCounter);
                    AddToFrequencySet(key, item.Frequency, item.AccessOrder);
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

        private void AddToFrequencySet(TKey key, int frequency, long accessOrder)
        {
            var tuple = new FrequencyKey(frequency, accessOrder);
            if (!_frequencySortedSet.TryGetValue(tuple, out var set))
            {
                set = new HashSet<TKey>();
                _frequencySortedSet[tuple] = set;
            }

            set.Add(key);
        }

        private void RemoveFromFrequencySet(TKey key, int frequency, long accessOrder)
        {
            var tuple = new FrequencyKey(frequency, accessOrder);
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

        private readonly record struct FrequencyKey(int Frequency, long AccessOrder) : IComparable<FrequencyKey>
        {
            public int CompareTo(FrequencyKey other)
            {
                var frequencyComparison = Frequency.CompareTo(other.Frequency);
                return frequencyComparison != 0
                           ? frequencyComparison
                           : AccessOrder.CompareTo(other.AccessOrder);
            }
        }

        private sealed class FrequencyItem(int frequency, long accessOrder)
        {
            public int Frequency { get; set; } = frequency;

            public long AccessOrder { get; set; } = accessOrder;
        }
    }
}
